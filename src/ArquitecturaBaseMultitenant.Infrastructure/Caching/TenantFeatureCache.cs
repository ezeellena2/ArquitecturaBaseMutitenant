using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Features;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>Guarda filas globales; el filtro evalúa su vencimiento en cada consulta.</summary>
internal sealed class TenantFeatureCache(HybridCache cache, IServiceScopeFactory scopes) : ITenantFeatureCache
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(5),
    };

    public async Task<IReadOnlyList<TenantFeatureRow>> ListAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await cache.GetOrCreateInOwnScopeAsync<ITenantFeatureReader, Guid, TenantFeatureRow[]>(
            CacheKeys.Tenant(tenantId, "features"), scopes, tenantId,
            static async (reader, id, ct) => (await reader.ListAsync(id, ct)).ToArray(), CacheOptions, cancellationToken);

    public ValueTask InvalidateAsync(Guid tenantId, CancellationToken cancellationToken) =>
        cache.RemoveAsync(CacheKeys.Tenant(tenantId, "features"), cancellationToken);
}
