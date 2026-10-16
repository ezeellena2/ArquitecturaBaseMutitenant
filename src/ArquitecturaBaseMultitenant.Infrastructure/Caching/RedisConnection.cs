using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>
/// La conexión compartida a Redis (ADR 0052): se abre una vez, sin esperar al servidor, y falla rápido mientras no
/// responde. Así una caída nunca frena el arranque ni demora un pedido más que el timeout de una operación; al volver,
/// reconecta sola. La comparten la caché y su health check.
/// </summary>
internal sealed class RedisConnection(IOptions<CacheOptions> options) : IDisposable, IAsyncDisposable
{
    private readonly Lazy<Task<IConnectionMultiplexer>> _connection = new(() => ConnectAsync(options.Value));

    internal Task<IConnectionMultiplexer> GetAsync() => _connection.Value;

    internal static ConfigurationOptions Configuration(CacheOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var configuration = ConfigurationOptions.Parse(options.ConnectionString);
        configuration.AbortOnConnectFail = false;
        configuration.BacklogPolicy = BacklogPolicy.FailFast;
        configuration.ConnectTimeout = options.ConnectTimeoutMilliseconds;
        configuration.SyncTimeout = options.OperationTimeoutMilliseconds;
        configuration.AsyncTimeout = options.OperationTimeoutMilliseconds;
        return configuration;
    }

    public void Dispose()
    {
        if (_connection.IsValueCreated && _connection.Value.IsCompletedSuccessfully)
            _connection.Value.Result.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection.IsValueCreated && _connection.Value.IsCompletedSuccessfully)
            await _connection.Value.Result.DisposeAsync();
    }

    private static async Task<IConnectionMultiplexer> ConnectAsync(CacheOptions options) =>
        await ConnectionMultiplexer.ConnectAsync(Configuration(options));
}
