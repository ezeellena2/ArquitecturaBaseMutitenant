using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>Invalida las preferencias y el nombre organizacional cacheados por TenantSettingsReader.</summary>
internal sealed class TenantSettingsCache(HybridCache cache) : ITenantSettingsCache
{
    public ValueTask InvalidateAsync(Guid tenantId, CancellationToken cancellationToken) =>
        cache.RemoveAsync(CacheKeys.Tenant(tenantId, "settings"), cancellationToken);
}
