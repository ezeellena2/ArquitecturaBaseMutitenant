using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>Estado global de una organización, con carga fuera del scope del pedido.</summary>
internal sealed class TenantStatusCache(HybridCache cache, IServiceScopeFactory scopes) : ITenantStatusCache
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromSeconds(60),
        LocalCacheExpiration = TimeSpan.FromSeconds(60),
    };

    public async Task<TenantStatus?> GetStatusAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var row = await cache.GetOrCreateInOwnScopeAsync<ITenantReader, Guid, TenantStatus?>(
            CacheKeys.Tenant(tenantId, "status"), scopes, tenantId,
            static async (reader, id, ct) => (await reader.FindByIdAsync(id, ct))?.Status,
            CacheOptions, cancellationToken);
        return row;
    }

    public ValueTask InvalidateAsync(Guid tenantId, CancellationToken cancellationToken) =>
        cache.RemoveAsync(CacheKeys.Tenant(tenantId, "status"), cancellationToken);
}
