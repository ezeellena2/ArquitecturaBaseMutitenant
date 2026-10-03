using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>Selecciona los ingresos verificados que siguen disponibles después de salir de una organización.</summary>
internal sealed class OrganizationAvailableLoginMethodsResolver(
    ILoginMethodRepository loginMethods,
    ILoginMethodMembershipReader memberships)
{
    internal async Task<IReadOnlyList<LoginMethod>> ResolveAsync(Guid userId, Guid tenantId,
        CancellationToken cancellationToken)
    {
        var available = new List<LoginMethod>();
        foreach (var method in await loginMethods.ListByUserIdAsync(userId, cancellationToken))
        {
            if (method.ManagedByTenantId == tenantId || method.InvitationOriginTenantId == tenantId) continue;
            if (method.ManagedByTenantId is { } ownerTenantId
                && !await memberships.IsActiveAsync(userId, ownerTenantId, cancellationToken))
                continue;
            available.Add(method);
        }
        return available;
    }
}
