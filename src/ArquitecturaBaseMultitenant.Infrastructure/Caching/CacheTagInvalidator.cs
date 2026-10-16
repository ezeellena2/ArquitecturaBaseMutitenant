using Microsoft.Extensions.Caching.Hybrid;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>
/// Después del commit, invalida en Redis las etiquetas cuyas generaciones avanzó la transacción (ADR 0052). No usa el
/// token del pedido: lo confirmado se invalida aunque el cliente se haya ido. Si Redis falla, nadie lee entradas viejas
/// igual, porque sus claves llevan las generaciones anteriores.
/// </summary>
internal sealed class CacheTagInvalidator(HybridCache cache)
{
    internal async Task InvalidateAsync(string[] tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        if (tags.Length == 0) return;
        using (CacheMetrics.BeginInvalidation())
            await cache.RemoveByTagAsync(tags, CancellationToken.None);
    }
}
