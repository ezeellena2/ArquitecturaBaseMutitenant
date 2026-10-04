using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Services.Roles;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>Valida roles por alcance y sincroniza asignaciones y membresías de una persona.</summary>
internal sealed class OrganizationUserAssignmentWriter(
    ICompanyReader companies,
    RoleAssignmentGuard roleGuard,
    IRoleAssignmentRepository assignments,
    ICompanyMembershipRepository companyMemberships,
    IMemberRepository members)
{
    internal async Task<Result<DesiredOrganizationUserAssignments>> ValidateAsync(
        IReadOnlyCollection<Guid> roleIds, IReadOnlyCollection<InviteCompanyRolesRequest> requestedCompanies,
        CancellationToken cancellationToken)
    {
        var organizationRoles = await roleGuard.CheckAsync(roleIds, companyId: null, cancellationToken);
        if (organizationRoles.IsFailure) return organizationRoles.Error;

        var companyRoles = new Dictionary<Guid, (CompanyRow Company, IReadOnlyList<RoleRow> Roles)>();
        foreach (var requested in requestedCompanies)
        {
            var company = await companies.FindByIdAsync(requested.CompanyId, cancellationToken);
            if (company is null) return CompanyErrors.NotFound;
            if (company.Status != CompanyStatus.Active) return CompanyErrors.Inactive;
            var selected = await roleGuard.CheckAsync(requested.RoleIds!, company.Id, cancellationToken);
            if (selected.IsFailure) return selected.Error;
            companyRoles.Add(company.Id, (company, selected.Value));
        }

        return new DesiredOrganizationUserAssignments(organizationRoles.Value, companyRoles);
    }

    internal async Task<OrganizationUserAssignmentSnapshot> GetSnapshotAsync(Guid memberId,
        CancellationToken cancellationToken) => new(
        await assignments.ListForMemberAsync(memberId, cancellationToken),
        await companyMemberships.ListForMemberAsync(memberId, cancellationToken));

    internal async Task<Result> SetAsync(Guid memberId, DesiredOrganizationUserAssignments desired,
        CancellationToken cancellationToken, OrganizationUserAssignmentSnapshot? snapshot = null,
        Member? memberInUnitOfWork = null)
    {
        var member = memberInUnitOfWork ?? await members.GetByIdAsync(memberId, cancellationToken);
        if (member is null) return MemberErrors.NotFound;
        snapshot ??= await GetSnapshotAsync(memberId, cancellationToken);

        var desiredAssignments = new List<RoleAssignment>();
        var organizationAssignments = await RecheckAsync(memberId, desired.OrganizationRoles, null,
            cancellationToken);
        if (organizationAssignments.IsFailure) return organizationAssignments.Error;
        desiredAssignments.AddRange(organizationAssignments.Value);
        foreach (var (companyId, selection) in desired.CompanyRoles)
        {
            var companyAssignments = await RecheckAsync(memberId, selection.Roles, companyId, cancellationToken);
            if (companyAssignments.IsFailure) return companyAssignments.Error;
            desiredAssignments.AddRange(companyAssignments.Value);
        }

        var desiredKeys = desiredAssignments.Select(AssignmentKey).ToHashSet();
        var existingKeys = snapshot.Assignments.Select(AssignmentKey).ToHashSet();
        var inactiveCompanyIds = snapshot.CompanyMemberships
            .Where(row => row.Status != CompanyMembershipStatus.Active)
            .Select(row => row.CompanyId)
            .ToHashSet();
        foreach (var assignment in snapshot.Assignments.Where(row => !desiredKeys.Contains(AssignmentKey(row))))
        {
            // Una edición general no reactiva empresas inactivas ni borra sus roles guardados.
            if (assignment.CompanyId is null || !inactiveCompanyIds.Contains(assignment.CompanyId.Value))
                assignments.Remove(assignment);
        }
        foreach (var assignment in desiredAssignments.Where(row => !existingKeys.Contains(AssignmentKey(row))))
            assignments.Add(assignment);

        var membershipsByCompany = snapshot.CompanyMemberships.ToDictionary(row => row.CompanyId);
        var desiredCompanyIds = desired.CompanyRoles.Keys.ToHashSet();
        foreach (var companyId in desiredCompanyIds)
        {
            if (membershipsByCompany.TryGetValue(companyId, out var membership))
            {
                if (membership.Status != CompanyMembershipStatus.Active)
                {
                    membership.SetStatus(CompanyMembershipStatus.Active);
                    companyMemberships.MarkUpdated(membership);
                }
            }
            else
            {
                var created = CompanyMembership.Create(companyId, memberId);
                if (created.IsFailure) return created.Error;
                companyMemberships.Add(created.Value);
            }
        }
        foreach (var membership in snapshot.CompanyMemberships.Where(row => row.Status == CompanyMembershipStatus.Active
                     && !desiredCompanyIds.Contains(row.CompanyId)))
        {
            membership.SetStatus(CompanyMembershipStatus.Inactive);
            companyMemberships.MarkUpdated(membership);
        }
        return Result.Success();
    }

    internal void Clear(OrganizationUserAssignmentSnapshot snapshot)
    {
        foreach (var assignment in snapshot.Assignments) assignments.Remove(assignment);
        foreach (var membership in snapshot.CompanyMemberships.Where(row => row.Status == CompanyMembershipStatus.Active))
        {
            membership.SetStatus(CompanyMembershipStatus.Inactive);
            companyMemberships.MarkUpdated(membership);
        }
    }

    // Adentro de la transacción cada rol se vuelve a controlar: pudo borrarse o cambiar de alcance desde la validación.
    private async Task<Result<RoleAssignment[]>> RecheckAsync(Guid memberId, IReadOnlyList<RoleRow> validated,
        Guid? companyId, CancellationToken cancellationToken)
    {
        var current = await roleGuard.CheckAsync(validated.Select(role => role.Id), companyId, cancellationToken);
        return current.IsFailure ? current.Error : RoleAssignmentGuard.Assign(memberId, current.Value, companyId);
    }

    private static (Guid RoleId, Guid? CompanyId) AssignmentKey(RoleAssignment assignment) =>
        (assignment.RoleId, assignment.CompanyId);
}
