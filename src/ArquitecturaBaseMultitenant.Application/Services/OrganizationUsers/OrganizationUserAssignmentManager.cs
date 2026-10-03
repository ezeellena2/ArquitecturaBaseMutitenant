using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>Valida roles por alcance y sincroniza asignaciones y membresías de una persona.</summary>
internal sealed class OrganizationUserAssignmentManager(
    ICompanyReader companies,
    IRoleReader roleReader,
    IRoleRepository roles,
    IRoleAssignmentRepository assignments,
    ICompanyMembershipRepository companyMemberships,
    IMemberRepository members)
{
    internal async Task<Result<DesiredOrganizationUserAssignments>> ValidateAsync(
        IReadOnlyCollection<Guid> roleIds, IReadOnlyCollection<InviteCompanyRolesRequest> requestedCompanies,
        CancellationToken cancellationToken)
    {
        var organizationRoles = new List<RoleRow>();
        foreach (var id in roleIds.Distinct())
        {
            var role = await roleReader.FindByIdAsync(id, cancellationToken);
            if (role is null) return RoleErrors.NotFound;
            if (role.Scope != RoleScope.Organization) return RoleErrors.CompanyNotAllowed;
            organizationRoles.Add(role);
        }

        var companyRoles = new Dictionary<Guid, (CompanyRow Company, IReadOnlyList<RoleRow> Roles)>();
        foreach (var requested in requestedCompanies)
        {
            var company = await companies.FindByIdAsync(requested.CompanyId, cancellationToken);
            if (company is null) return CompanyErrors.NotFound;
            if (company.Status != CompanyStatus.Active) return CompanyErrors.Inactive;
            var selected = new List<RoleRow>();
            foreach (var roleId in requested.RoleIds!.Distinct())
            {
                var role = await roleReader.FindByIdAsync(roleId, cancellationToken);
                if (role is null) return RoleErrors.NotFound;
                if (role.Scope == RoleScope.Organization) return RoleAssignmentErrors.CompanyNotAllowed;
                if (role.Scope == RoleScope.SpecificCompany && role.CompanyId != company.Id)
                    return RoleAssignmentErrors.CompanyMismatch;
                selected.Add(role);
            }
            companyRoles.Add(company.Id, (company, selected));
        }

        return new DesiredOrganizationUserAssignments(organizationRoles, companyRoles);
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
        foreach (var roleRow in desired.OrganizationRoles)
        {
            var role = await roles.GetByIdAsync(roleRow.Id, roleRow.Version, cancellationToken);
            if (role is null) return RoleErrors.NotFound;
            if (role.Scope != RoleScope.Organization) return RoleErrors.CompanyNotAllowed;
            var assignment = RoleAssignment.Assign(memberId, role, companyId: null);
            if (assignment.IsFailure) return assignment.Error;
            desiredAssignments.Add(assignment.Value);
        }
        foreach (var (companyId, selection) in desired.CompanyRoles)
        foreach (var roleRow in selection.Roles)
        {
            var role = await roles.GetByIdAsync(roleRow.Id, roleRow.Version, cancellationToken);
            if (role is null) return RoleErrors.NotFound;
            if (role.Scope == RoleScope.Organization) return RoleAssignmentErrors.CompanyNotAllowed;
            if (role.Scope == RoleScope.SpecificCompany && role.CompanyId != companyId)
                return RoleAssignmentErrors.CompanyMismatch;
            var assignment = RoleAssignment.Assign(memberId, role, companyId);
            if (assignment.IsFailure) return assignment.Error;
            desiredAssignments.Add(assignment.Value);
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

    private static (Guid RoleId, Guid? CompanyId) AssignmentKey(RoleAssignment assignment) =>
        (assignment.RoleId, assignment.CompanyId);
}
