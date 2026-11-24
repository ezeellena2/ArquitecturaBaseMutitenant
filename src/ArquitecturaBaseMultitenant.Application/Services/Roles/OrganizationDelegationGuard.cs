using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Roles;

/// <summary>
/// Nadie da lo que no tiene, en la organización (ADR 0061): lo usan <c>RoleService</c> y los casos de Usuarios (invitar,
/// editar, cambiar el estado y quitar), adentro del límite del caso de uso y después de sus locks, y decide con
/// <see cref="OrganizationDelegationPolicy"/>. Lo que tiene quien pide se lee de la base en cada llamada, con la misma
/// proyección que la caché de permisos que mira <c>PermissionAuthorizationHandler</c> (sus roles de organización y los de
/// cada empresa activa donde tiene la membresía activa), y no de lo que el pedido ya memorizó. Cuenta solo lo efectivo
/// del catálogo vigente (decisión 260): un permiso de un módulo quitado no da ni pide nada.
/// </summary>
internal sealed class OrganizationDelegationGuard(ICurrentUser currentUser, IRoleReader roles)
{
    /// <summary>Lo que tiene quien pide, en la organización y en cada empresa, leído ahora.</summary>
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
        var permissions = await PermissionsOfAsync(assignments, cancellationToken);
        return PermissionsByPlace.Of(
            assignments.Where(assignment => assignment.CompanyId is null).SelectMany(Of),
            assignments.Where(assignment => assignment.CompanyId is not null)
                .GroupBy(assignment => assignment.CompanyId!.Value)
                .ToDictionary(company => company.Key, company => company.SelectMany(Of)));

        IEnumerable<string> Of(RoleAssignment assignment) => permissions.GetValueOrDefault(assignment.RoleId) ?? [];
    }

    /// <summary>
    /// Crear o editar un rol: quien pide tiene cada permiso que entra o sale; si no,
    /// <see cref="RoleErrors.PermissionsNotHeld"/>.
    /// </summary>
    internal async Task<Result> CheckPermissionsChangeAsync(IEnumerable<string> before, IEnumerable<string> after,
        CancellationToken cancellationToken) =>
        OrganizationDelegationPolicy.CanChangePermissions(await ReadHeldAsync(cancellationToken), before, after)
            ? Result.Success()
            : RoleErrors.PermissionsNotHeld;

    /// <summary>
    /// Dar a una persona los roles de <paramref name="added"/> y cambiar las asignaciones guardadas de
    /// <paramref name="saved"/> (las que salen, o las que vuelven a valer): cada rol permite solo lo que quien pide tiene
    /// donde se da o se quita; si no, <see cref="MemberErrors.RolesNotHeld"/>.
    /// </summary>
    internal async Task<Result> CheckRolesChangeAsync(IReadOnlyCollection<RoleAssignment> saved,
        IReadOnlyCollection<RoleGrant> added, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(added);
        if (saved.Count == 0 && added.Count == 0) return Result.Success();
        var permissions = await PermissionsOfAsync(saved, cancellationToken);
        RoleGrant[] changed =
        [
            .. added,
            .. saved.Select(assignment => new RoleGrant(
                permissions.GetValueOrDefault(assignment.RoleId) ?? [], assignment.CompanyId)),
        ];
        return OrganizationDelegationPolicy.CanChangeRoles(await ReadHeldAsync(cancellationToken), changed)
            ? Result.Success()
            : MemberErrors.RolesNotHeld;
    }

    /// <summary>
    /// Actuar sobre <paramref name="member"/>, con todas sus <paramref name="assignments"/>: quien pide tiene todo lo que
    /// le dan; si no, <see cref="MemberErrors.HasMorePermissions"/>. Sobre uno mismo siempre.
    /// </summary>
    internal async Task<Result> CheckActOnAsync(Member member, IReadOnlyCollection<RoleAssignment> assignments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(member);
        var self = member.UserId is { } userId && userId == currentUser.UserId;
        if (self || assignments.Count == 0) return Result.Success();
        return OrganizationDelegationPolicy.CanActOn(self, await ReadHeldAsync(cancellationToken),
                await ReadAssignedAsync(assignments, cancellationToken))
            ? Result.Success()
            : MemberErrors.HasMorePermissions;
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> PermissionsOfAsync(
        IEnumerable<RoleAssignment> assignments, CancellationToken cancellationToken) =>
        await roles.ListPermissionsByRoleAsync([.. assignments.Select(assignment => assignment.RoleId).Distinct()],
            cancellationToken);
}
