using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>Cachea permisos por organización y rol; la fábrica lee RLS en un scope tenant-scoped propio.</summary>
internal sealed class RolePermissionCache(HybridCache cache, IServiceScopeFactory scopes) : IRolePermissionCache
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromHours(1),
    };

    public async Task<IReadOnlyList<string>> GetRolePermissionsAsync(Guid tenantId, Guid roleId,
        CancellationToken cancellationToken) =>
        await cache.GetOrCreateInOwnScopeAsync<(Guid TenantId, Guid RoleId), string[]>(
            CacheKey(tenantId, roleId), scopes, (tenantId, roleId),
            static async (services, state, ct) =>
            {
                services.GetRequiredService<ITenantAccessInitializer>()
                    .SetFromAccess(state.TenantId, TenantKind.Business);
                return [.. await services.GetRequiredService<IRoleReader>()
                    .ListPermissionsForRoleAsync(state.RoleId, ct)];
            }, CacheOptions, cancellationToken);

    public async Task InvalidateRoleAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken) =>
        await cache.RemoveAsync(CacheKey(tenantId, roleId), cancellationToken);

    private static string CacheKey(Guid tenantId, Guid roleId) =>
        CacheKeys.Tenant(tenantId, "role-permissions:" + roleId.ToString("N", System.Globalization.CultureInfo.InvariantCulture));
}
