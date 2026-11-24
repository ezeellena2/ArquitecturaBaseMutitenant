using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Services.Roles;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>
/// Valida roles por alcance y sincroniza asignaciones y membresías de una persona. Antes de escribir, cada rol que entra
/// o sale pasa por <see cref="OrganizationDelegationGuard"/> (ADR 0061): invitar, invitar por teléfono y editar. A un
/// invitado se le da cada rol pedido, aunque ya figure: lo que tiene guardado no vale hasta que acepte.
/// </summary>
internal sealed class OrganizationUserAssignmentWriter(
    ICompanyReader companies,
    RoleAssignmentGuard roleGuard,
    OrganizationDelegationGuard delegation,
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
        var desiredPermissions = new Dictionary<(Guid RoleId, Guid? CompanyId), IReadOnlyCollection<string>>();
        var organizationAssignments = await RecheckAsync(memberId, desired.OrganizationRoles, null,
            cancellationToken);
        if (organizationAssignments.IsFailure) return organizationAssignments.Error;
        Collect(organizationAssignments.Value);
        foreach (var (companyId, selection) in desired.CompanyRoles)
        {
            var companyAssignments = await RecheckAsync(memberId, selection.Roles, companyId, cancellationToken);
            if (companyAssignments.IsFailure) return companyAssignments.Error;
            Collect(companyAssignments.Value);
        }

        var desiredKeys = desiredAssignments.Select(AssignmentKey).ToHashSet();
        var existingKeys = snapshot.Assignments.Select(AssignmentKey).ToHashSet();
        var inactiveCompanyIds = snapshot.CompanyMemberships
            .Where(row => row.Status != CompanyMembershipStatus.Active)
            .Select(row => row.CompanyId)
            .ToHashSet();
        // Una edición general no reactiva empresas inactivas ni borra sus roles guardados.
        var removed = snapshot.Assignments.Where(row => !desiredKeys.Contains(AssignmentKey(row))
            && (row.CompanyId is null || !inactiveCompanyIds.Contains(row.CompanyId.Value))).ToArray();
        var added = desiredAssignments.Where(row => !existingKeys.Contains(AssignmentKey(row))).ToArray();
        // Una empresa inactiva que vuelve con la edición reactiva también los roles que guardaba: vuelven a darse.
        var revived = snapshot.Assignments.Where(row => !desiredKeys.Contains(AssignmentKey(row))
            && row.CompanyId is { } companyId && inactiveCompanyIds.Contains(companyId)
            && desired.CompanyRoles.ContainsKey(companyId)).ToArray();
        // ADR 0061: cada rol que entra, sale o vuelve permite solo lo que quien pide tiene donde se da. Lo guardado en un
        // invitado no vale todavía (pudo quedar de una invitación revocada): lo da la invitación, así que se da todo.
        RoleAssignment[] granted = member.Status == MemberStatus.Invited ? [.. desiredAssignments] : added;
        var delegated = await delegation.CheckRolesChangeAsync([.. removed, .. revived],
            [.. granted.Select(row => new RoleGrant(desiredPermissions[AssignmentKey(row)], row.CompanyId))],
            cancellationToken);
        if (delegated.IsFailure) return delegated;
        foreach (var assignment in removed) assignments.Remove(assignment);
        foreach (var assignment in added) assignments.Add(assignment);

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

        void Collect(IEnumerable<(RoleAssignment Assignment, IReadOnlyCollection<string> Permissions)> rechecked)
        {
            foreach (var (assignment, permissions) in rechecked)
            {
                desiredAssignments.Add(assignment);
                desiredPermissions[AssignmentKey(assignment)] = permissions;
            }
        }
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

    // Adentro de la transacción cada rol se vuelve a controlar: pudo borrarse, cambiar de alcance o de permisos desde la
    // validación. Vuelve cada asignación con los permisos del rol leído ahora.
    private async Task<Result<(RoleAssignment Assignment, IReadOnlyCollection<string> Permissions)[]>> RecheckAsync(
        Guid memberId, IReadOnlyList<RoleRow> validated, Guid? companyId, CancellationToken cancellationToken)
    {
        var current = await roleGuard.CheckAsync(validated.Select(role => role.Id), companyId, cancellationToken);
        if (current.IsFailure) return current.Error;
        var assigned = RoleAssignmentGuard.Assign(memberId, current.Value, companyId);
        if (assigned.IsFailure) return assigned.Error;
        var permissions = current.Value.ToDictionary(role => role.Id, role => role.Permissions);
        return assigned.Value.Select(assignment =>
            (assignment, (IReadOnlyCollection<string>)permissions[assignment.RoleId])).ToArray();
    }

    private static (Guid RoleId, Guid? CompanyId) AssignmentKey(RoleAssignment assignment) =>
        (assignment.RoleId, assignment.CompanyId);
}
