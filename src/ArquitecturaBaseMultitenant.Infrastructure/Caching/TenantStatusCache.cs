using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>Estado global de una organización en la caché compartida (ADR 0052): 60 s, etiqueta de la organización.</summary>
internal sealed class TenantStatusCache(SharedCache cache) : ITenantStatusCache
{
    private static readonly TimeSpan Expiration = TimeSpan.FromSeconds(60);

    public Task<TenantStatus?> GetStatusAsync(Guid tenantId, CancellationToken cancellationToken) =>
        cache.GetOrLoadAsync(
            new CacheEntry(CacheKeys.Tenant(tenantId, "status"), [CacheTags.Organization(tenantId)], Expiration),
            tenantId,
            static async (services, id, ct) =>
                (await services.GetRequiredService<ITenantReader>().FindByIdAsync(id, ct))?.Status,
            cancellationToken);
}
