using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>
/// La única puerta a la caché compartida (ADR 0052). Lee de PostgreSQL, en una consulta y sin caché, las generaciones de
/// las etiquetas de la entrada; con ellas y la versión del esquema arma la clave física. Un miss carga desde un scope
/// propio y vuelve a leer las generaciones: si cambiaron en el medio, el sobre lleva las nuevas y quien lo pidió lo
/// descarta, así nunca se etiquetan datos anteriores con una generación posterior. Con cambios continuos entrega una
/// lectura directa, sin llenar la caché.
/// </summary>
internal sealed class SharedCache(HybridCache cache, IServiceScopeFactory scopes, CacheMetrics metrics)
{
    internal const int SchemaVersion = 1;
    private const string SchemaPrefix = "v1";
    private const int Attempts = 3;

    internal async Task<TValue> GetOrLoadAsync<TState, TValue>(CacheEntry entry, TState state,
        Func<IServiceProvider, TState, CancellationToken, Task<TValue>> load, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(load);
        var options = new HybridCacheEntryOptions
        {
            Expiration = entry.Expiration,
            // Redis es la única caché: ninguna instancia guarda una copia propia que otra no pueda invalidar.
            Flags = HybridCacheEntryFlags.DisableLocalCache,
        };
        string[] versioned = [CacheTags.Database, .. entry.Tags];
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            string generations;
            await using (var scope = scopes.CreateAsyncScope())
                generations = await ReadGenerationsAsync(scope.ServiceProvider, versioned, cancellationToken);
            var key = Key(entry, generations);
            var fill = new Fill<TState, TValue>(versioned, state, load);
            var envelope = await cache.GetOrCreateInOwnScopeAsync(key, scopes, fill, Fill<TState, TValue>.LoadAsync,
                options, entry.Tags, cancellationToken);
            if (envelope is { SchemaVersion: SchemaVersion }
                && string.Equals(envelope.Generations, generations, StringComparison.Ordinal))
            {
                metrics.Read(fill.Loaded);
                return envelope.Value;
            }

            await cache.RemoveAsync(key, cancellationToken);
        }

        metrics.DirectRead();
        await using var direct = scopes.CreateAsyncScope();
        return await load(direct.ServiceProvider, state, cancellationToken);
    }

    /// <summary>La clave física, sin el prefijo de la aplicación y el ambiente que agrega Redis.</summary>
    internal static string Key(CacheEntry entry, string generations) =>
        SchemaPrefix + ":" + entry.Key + ":g" + generations;

    private static async Task<string> ReadGenerationsAsync(IServiceProvider services, string[] tags,
        CancellationToken cancellationToken)
    {
        var versions = await services.GetRequiredService<ApplicationDbContext>().CacheTagVersions.AsNoTracking()
            .Where(version => tags.Contains(version.Tag))
            .ToDictionaryAsync(version => version.Tag, version => version.Generation, StringComparer.Ordinal,
                cancellationToken);
        return CacheKeyFormat.Generations([.. tags.Select(tag => versions.GetValueOrDefault(tag))]);
    }

    /// <summary>La carga de un miss, con lo que necesita para comprobar después las generaciones.</summary>
    private sealed class Fill<TState, TValue>(string[] tags, TState state,
        Func<IServiceProvider, TState, CancellationToken, Task<TValue>> load)
    {
        private string[] Tags { get; } = tags;

        private TState State { get; } = state;

        private Func<IServiceProvider, TState, CancellationToken, Task<TValue>> Load { get; } = load;

        internal bool Loaded { get; private set; }

        internal static async Task<CacheEnvelope<TValue>> LoadAsync(IServiceProvider services, Fill<TState, TValue> fill,
            CancellationToken cancellationToken)
        {
            var value = await fill.Load(services, fill.State, cancellationToken);
            var generations = await ReadGenerationsAsync(services, fill.Tags, cancellationToken);
            fill.Loaded = true;
            return new CacheEnvelope<TValue>(SchemaVersion, generations, value);
        }
    }
}
