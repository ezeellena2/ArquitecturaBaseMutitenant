using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>
/// La fábrica que llena HybridCache lee en un scope propio, con su contexto y su conexión. La usa sólo
/// <see cref="SharedCache"/>; TransactionBoundaryTests vigila que nadie más llene la caché.
/// </summary>
internal static class HybridCacheExtensions
{
    /// <summary>
    /// Lo cacheado en <paramref name="key"/>, o lo que lee <paramref name="read"/> desde un scope nuevo. Sobre el contexto
    /// de quien llama, la fábrica correría adentro de su límite: vería lo que todavía no se confirmó y lo cachearía. Con la
    /// protección contra estampidas, además, la fábrica sigue sirviendo a otros pedidos después de que el que la arrancó
    /// terminó o se canceló: le ocuparía la conexión que su rollback necesita para soltar los locks y moriría con su
    /// scope. El precio es que, con el caché frío, la fábrica pide una segunda conexión al pool mientras quien llama tiene
    /// la suya tomada por su límite. Si el pool se agotara con límites que esperan justo esta fábrica, ella esperaría el
    /// timeout de conexión y todos esos pedidos terminarían en un 500. Con una fábrica por clave y entradas por
    /// organización o persona es improbable; no sirve para una fábrica que corra por fila.
    /// </summary>
    public static ValueTask<TValue> GetOrCreateInOwnScopeAsync<TState, TValue>(
        this HybridCache cache,
        string key,
        IServiceScopeFactory scopes,
        TState state,
        Func<IServiceProvider, TState, CancellationToken, Task<TValue>> read,
        HybridCacheEntryOptions options,
        IEnumerable<string> tags,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cache);

        return cache.GetOrCreateAsync(
            key,
            (scopes, state, read),
            static async (factory, token) =>
            {
                await using var scope = factory.scopes.CreateAsyncScope();
                return await factory.read(scope.ServiceProvider, factory.state, token);
            },
            options,
            tags,
            cancellationToken);
    }
}
