using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Organization;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Application.Services.Roles;

/// <summary>
/// Avisa a los <see cref="IMemberAccessObserver"/>, dentro de la UoW del núcleo, qué permisos de organización le quedan a cada
/// persona cuyo acceso cambió (B12 de los arreglos de la revisión): una sola llamada por caso, con todas las personas, así un
/// observador lee y bloquea de una vez lo de todas. Lo efectivo sale de <see cref="Permissions.OrganizationEffective"/>, lo
/// mismo que lee <see cref="PermissionService"/>.
/// </summary>
internal sealed class MemberAccessNotifier(IRoleReader roles, IEnumerable<IMemberAccessObserver> observers)
{
    /// <summary>Lo que le queda a cada persona, ya calculado (vacío si dejó de estar activa).</summary>
    internal async Task NotifyAsync(IReadOnlyDictionary<Guid, IReadOnlyCollection<string>> effectiveByUser, CancellationToken ct)
    {
        if (effectiveByUser.Count == 0) return;
        foreach (var observer in observers) await observer.AccessChangedAsync(effectiveByUser, ct);
    }

    /// <summary>Los roles de una persona activa, con lo que dan los de organización y lo que proyecta cada empresa.</summary>
    internal Task NotifyAsync(Guid userId, IEnumerable<IReadOnlyCollection<string>> organizationRoles,
        IEnumerable<IReadOnlyCollection<string>> companyRoles, CancellationToken ct) =>
        NotifyAsync(new Dictionary<Guid, IReadOnlyCollection<string>>
        {
            [userId] = Permissions.OrganizationEffective(organizationRoles.SelectMany(permissions => permissions), companyRoles),
        }, ct);

    /// <summary>
    /// Un rol de organización perdió permisos: lo efectivo de cada persona activa que lo tiene, con el rol editado según sus
    /// permisos nuevos, todavía sin guardar. Lee las personas y los permisos de sus otros roles en un número fijo de consultas.
    /// </summary>
    internal async Task NotifyRoleChangedAsync(Role role, IReadOnlyCollection<string> previousPermissions, CancellationToken ct)
    {
        if (role.Scope != RoleScope.Organization || !observers.Any()
            || !previousPermissions.Except(role.Permissions, StringComparer.Ordinal).Any()) return;
        var holders = await roles.ListActiveHoldersAsync(role.Id, ct);
        if (holders.Count == 0) return;
        var otherRoleIds = holders
            .SelectMany(holder => holder.OrganizationRoleIds.Concat(holder.CompanyRoleIds.Values.SelectMany(ids => ids)))
            .Where(id => id != role.Id).Distinct().ToArray();
        var granted = await roles.ListPermissionsByRoleAsync(otherRoleIds, ct);
        IEnumerable<string> Of(Guid roleId) => roleId == role.Id ? role.Permissions : granted.GetValueOrDefault(roleId) ?? [];
        await NotifyAsync(holders.ToDictionary(holder => holder.UserId,
            holder => (IReadOnlyCollection<string>)Permissions.OrganizationEffective(holder.OrganizationRoleIds.SelectMany(Of),
                holder.CompanyRoleIds.Values.Select(ids => ids.SelectMany(Of)))), ct);
    }
}
