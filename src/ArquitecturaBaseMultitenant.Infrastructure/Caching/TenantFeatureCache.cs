using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Features;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>
/// Las habilitaciones globales de cada espacio en la caché compartida (ADR 0052), 5 min; el filtro evalúa su
/// vencimiento en cada consulta con TimeProvider.
/// </summary>
internal sealed class TenantFeatureCache(SharedCache cache) : ITenantFeatureCache
{
    private static readonly TimeSpan Expiration = TimeSpan.FromMinutes(5);

    public async Task<IReadOnlyList<TenantFeatureRow>> ListAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await cache.GetOrLoadAsync(
            new CacheEntry(CacheKeys.Tenant(tenantId, "features"), [CacheTags.Features(tenantId)], Expiration),
            tenantId,
            static async (services, id, ct) =>
                (await services.GetRequiredService<ITenantFeatureReader>().ListAsync(id, ct)).ToArray(),
            cancellationToken);
}
