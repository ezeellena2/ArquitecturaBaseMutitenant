using Microsoft.Extensions.Caching.Hybrid;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>Claves globales de los cinco catálogos e invalidación después del commit del seed.</summary>
internal sealed class ReferenceDataCache(HybridCache cache)
{
    internal static readonly string CurrenciesKey = CacheKeys.Platform("ref:currencies");
    internal static readonly string CountriesKey = CacheKeys.Platform("ref:countries");
    internal static readonly string TimeZonesKey = CacheKeys.Platform("ref:time-zones");
    internal static readonly string CulturesKey = CacheKeys.Platform("ref:cultures");
    internal static readonly string TaxIdTypesKey = CacheKeys.Platform("ref:tax-id-types");

    public async ValueTask InvalidateAsync(CancellationToken cancellationToken)
    {
        await cache.RemoveAsync(CurrenciesKey, cancellationToken);
        await cache.RemoveAsync(CountriesKey, cancellationToken);
        await cache.RemoveAsync(TimeZonesKey, cancellationToken);
        await cache.RemoveAsync(CulturesKey, cancellationToken);
        await cache.RemoveAsync(TaxIdTypesKey, cancellationToken);
    }
}
