using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Application.Services.Roles;

/// <summary>
/// Lo que tiene quien pide y lo que dan unas asignaciones, para que decidan las <c>Policy</c> de la 0061: lo usan
/// <see cref="OrganizationDelegationGuard"/>, <c>CompanyDelegationGuard</c>, <see cref="RoleService"/> y las fuentes del
/// asistente, que no pueden depender de un <c>Guard</c> (ADR 0068 §5). Solo lee, con <see cref="ICurrentUser"/> e
/// <see cref="IRoleReader"/>. Lo que tiene quien pide se lee de la base en cada llamada, con la misma proyección que la
/// caché de permisos que mira <c>PermissionAuthorizationHandler</c> (sus roles de organización y los de cada empresa
/// activa donde tiene la membresía activa), y no de lo que el pedido ya memorizó. Cuenta solo lo efectivo del catálogo
/// vigente (decisión 260): un permiso de un módulo quitado no da ni pide nada.
/// </summary>
internal sealed class DelegationPermissionsResolver(ICurrentUser currentUser, IRoleReader roles)
{
    /// <summary>Lo que tiene quien pide, en la organización y en cada empresa, leído ahora; sin sesión, nada.</summary>
    internal async Task<PermissionsByPlace> ReadHeldAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } actorId || actorId == Guid.Empty) return PermissionsByPlace.None;
        var organizationRoleIds = await roles.ListOrganizationRoleIdsAsync(actorId, cancellationToken);
        var companyRoleIds = await roles.ListCompanyRoleIdsAsync(actorId, cancellationToken);
        var permissions = await roles.ListPermissionsByRoleAsync(
            [.. organizationRoleIds.Concat(companyRoleIds.Values.SelectMany(ids => ids)).Distinct()], cancellationToken);
        return PermissionsByPlace.Of(organizationRoleIds.SelectMany(Of),
            companyRoleIds.ToDictionary(company => company.Key, company => company.Value.SelectMany(Of)));

        IEnumerable<string> Of(Guid roleId) => permissions.GetValueOrDefault(roleId) ?? [];
    }

    /// <summary>Lo que dan estas asignaciones, por lugar, sin mirar el estado del miembro, la membresía ni la empresa.</summary>
    internal async Task<PermissionsByPlace> ReadAssignedAsync(IReadOnlyCollection<RoleAssignment> assignments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        var permissions = await roles.ListPermissionsByRoleAsync(
            [.. assignments.Select(assignment => assignment.RoleId).Distinct()], cancellationToken);
        return PermissionsByPlace.Of(
            assignments.Where(assignment => assignment.CompanyId is null).SelectMany(Of),
            assignments.Where(assignment => assignment.CompanyId is not null)
                .GroupBy(assignment => assignment.CompanyId!.Value)
                .ToDictionary(company => company.Key, company => company.SelectMany(Of)));

        IEnumerable<string> Of(RoleAssignment assignment) => permissions.GetValueOrDefault(assignment.RoleId) ?? [];
    }
}
