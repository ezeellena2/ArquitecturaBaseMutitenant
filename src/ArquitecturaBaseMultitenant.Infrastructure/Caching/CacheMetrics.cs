using System.Diagnostics.Metrics;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>
/// Métricas de la caché compartida (ADR 0052): aciertos, misses, lecturas directas, caídas de Redis e invalidaciones
/// fallidas. Sin claves ni identificadores en sus dimensiones.
/// </summary>
internal sealed class CacheMetrics
{
    internal const string MeterName = "ArquitecturaBaseMultitenant.Cache";

    // Una invalidación de HybridCache escribe en Redis; su fallo cuenta como invalidación, no como escritura.
    private static readonly AsyncLocal<bool> Invalidation = new();

    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;
    private readonly Counter<long> _directReads;
    private readonly Counter<long> _fallbacks;
    private readonly Counter<long> _invalidationFailures;

    public CacheMetrics(IMeterFactory meters)
    {
        ArgumentNullException.ThrowIfNull(meters);
        var meter = meters.Create(MeterName);
        _hits = meter.CreateCounter<long>("arquitecturabasemt.cache.hits");
        _misses = meter.CreateCounter<long>("arquitecturabasemt.cache.misses");
        _directReads = meter.CreateCounter<long>("arquitecturabasemt.cache.direct_reads");
        _fallbacks = meter.CreateCounter<long>("arquitecturabasemt.cache.fallbacks");
        _invalidationFailures = meter.CreateCounter<long>("arquitecturabasemt.cache.invalidation_failures");
    }

    internal static bool Invalidating => Invalidation.Value;

    internal void Read(bool loaded) => (loaded ? _misses : _hits).Add(1);

    internal void DirectRead() => _directReads.Add(1);

    internal void Fallback() => _fallbacks.Add(1);

    internal void InvalidationFailed() => _invalidationFailures.Add(1);

    /// <summary>Marca lo que corre adentro como invalidación, hasta que se descarta.</summary>
    internal static IDisposable BeginInvalidation()
    {
        Invalidation.Value = true;
        return new InvalidationScope();
    }

    private sealed class InvalidationScope : IDisposable
    {
        public void Dispose() => Invalidation.Value = false;
    }
}
