using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

internal sealed class PlatformSettingsCache(HybridCache cache) : IPlatformSettingsCache
{
    public ValueTask InvalidateAsync(CancellationToken cancellationToken) =>
        cache.RemoveAsync(CacheKeys.Platform("settings"), cancellationToken);
}
