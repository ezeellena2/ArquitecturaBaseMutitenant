using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Companies;

/// <summary>Valida roles de empresa y sincroniza sus asignaciones sin tocar otros alcances.</summary>
internal sealed class CompanyMembershipRoleManager(
    IRoleRepository roles,
    IRoleReader roleReader,
    IRoleAssignmentRepository assignments)
{
    public async Task<Result<Guid[]>> ValidateRoleIdsAsync(Guid companyId, Guid[] roleIds,
        CancellationToken cancellationToken)
    {
        var normalized = roleIds.Distinct().ToArray();
        foreach (var roleId in normalized)
        {
            var role = await roleReader.FindByIdAsync(roleId, cancellationToken);
            if (role is null) return RoleErrors.NotFound;
            if (role.Scope == RoleScope.Organization) return RoleErrors.CompanyNotAllowed;
            if (role.Scope == RoleScope.SpecificCompany && role.CompanyId != companyId)
                return RoleAssignmentErrors.CompanyMismatch;
        }

        return normalized;
    }

    public async Task<Result<RoleAssignment[]>> CreateAssignmentsAsync(Guid memberId, Guid companyId,
        Guid[] roleIds, CancellationToken cancellationToken)
    {
        var result = new List<RoleAssignment>(roleIds.Length);
        foreach (var roleId in roleIds)
        {
            var row = await roleReader.FindByIdAsync(roleId, cancellationToken);
            if (row is null) return RoleErrors.NotFound;
            var role = await roles.GetByIdAsync(roleId, row.Version, cancellationToken);
            if (role is null) return RoleErrors.NotFound;
            if (role.Scope == RoleScope.Organization) return RoleErrors.CompanyNotAllowed;
            if (role.Scope == RoleScope.SpecificCompany && role.CompanyId != companyId)
                return RoleAssignmentErrors.CompanyMismatch;
            var assignment = RoleAssignment.Assign(memberId, role, companyId);
            if (assignment.IsFailure) return assignment.Error;
            result.Add(assignment.Value);
        }

        return result.ToArray();
    }

    public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken) =>
        roles.GetByNameAsync(name, cancellationToken);

    public Task<IReadOnlyList<RoleAssignment>> ListForMemberAsync(Guid memberId,
        CancellationToken cancellationToken) => assignments.ListForMemberAsync(memberId, cancellationToken);

    public void Add(RoleAssignment assignment) => assignments.Add(assignment);

    public void Remove(RoleAssignment assignment) => assignments.Remove(assignment);
}
