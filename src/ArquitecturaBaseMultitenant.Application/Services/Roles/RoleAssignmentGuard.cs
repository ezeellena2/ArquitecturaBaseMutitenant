using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Roles;

/// <summary>
/// Protege dónde vale cada rol al asignarlo, en Usuarios y en Empresas: el rol existe en la organización y
/// <see cref="RoleAssignment.Check"/> admite la empresa. Responde los mismos códigos desde las dos áreas.
/// </summary>
internal sealed class RoleAssignmentGuard(IRoleReader roles)
{
    /// <summary>Los roles pedidos, sin repetir, si todos valen en <paramref name="companyId"/> (null: la organización).</summary>
    public async Task<Result<IReadOnlyList<RoleRow>>> CheckAsync(IEnumerable<Guid> roleIds, Guid? companyId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(roleIds);
        var checkedRoles = new List<RoleRow>();
        foreach (var roleId in roleIds.Distinct())
        {
            var role = await roles.FindByIdAsync(roleId, cancellationToken);
            if (role is null) return RoleErrors.NotFound;
            if (RoleAssignment.Check(role.Scope, role.CompanyId, companyId) is { } error) return error;
            checkedRoles.Add(role);
        }

        return checkedRoles;
    }

    /// <summary>Las asignaciones de roles ya controlados, sin volver a leerlos.</summary>
    public static Result<RoleAssignment[]> Assign(Guid memberId, IEnumerable<RoleRow> checkedRoles, Guid? companyId)
    {
        ArgumentNullException.ThrowIfNull(checkedRoles);
        var assignments = new List<RoleAssignment>();
        foreach (var role in checkedRoles)
        {
            var assignment = RoleAssignment.Assign(memberId, role.Id, role.Scope, role.CompanyId, companyId);
            if (assignment.IsFailure) return assignment.Error;
            assignments.Add(assignment.Value);
        }

        return assignments.ToArray();
    }
}
