using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>
/// Redis detrás de HybridCache (ADR 0052). Una caída, un timeout o un error de Redis nunca rompen un pedido: la lectura
/// vuelve a PostgreSQL como un miss y la escritura o la invalidación se pierden, sin quitar corrección porque las
/// generaciones ya avanzaron. La cancelación del pedido sí se propaga.
/// </summary>
internal sealed partial class ResilientDistributedCache(IDistributedCache redis, CacheMetrics metrics,
    ILogger<ResilientDistributedCache> logger) : IDistributedCache
{
    public byte[]? Get(string key)
    {
        try
        {
            return redis.Get(key);
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            ReadFailed(exception);
            return null;
        }
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        try
        {
            return await redis.GetAsync(key, token);
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            ReadFailed(exception);
            return null;
        }
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        try
        {
            redis.Set(key, value, options);
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            WriteFailed(exception);
        }
    }

    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
        CancellationToken token = default)
    {
        try
        {
            await redis.SetAsync(key, value, options, token);
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            WriteFailed(exception);
        }
    }

    public void Refresh(string key)
    {
        try
        {
            redis.Refresh(key);
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            ReadFailed(exception);
        }
    }

    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
        try
        {
            await redis.RefreshAsync(key, token);
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            ReadFailed(exception);
        }
    }

    public void Remove(string key)
    {
        try
        {
            redis.Remove(key);
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            InvalidationFailed(exception);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        try
        {
            await redis.RemoveAsync(key, token);
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            InvalidationFailed(exception);
        }
    }

    // RedisTimeoutException es un TimeoutException; la conexión, la autenticación y el servidor, RedisException.
    private static bool IsRedisFailure(Exception exception) => exception is RedisException or TimeoutException;

    private void ReadFailed(Exception exception)
    {
        metrics.Fallback();
        LogReadFallback(logger, exception.GetType().Name);
    }

    private void WriteFailed(Exception exception)
    {
        if (CacheMetrics.Invalidating)
        {
            InvalidationFailed(exception);
            return;
        }

        metrics.Fallback();
        LogWriteSkipped(logger, exception.GetType().Name);
    }

    private void InvalidationFailed(Exception exception)
    {
        metrics.InvalidationFailed();
        LogInvalidationFailed(logger, exception.GetType().Name);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Shared cache read fell back to the database: {ExceptionType}")]
    private static partial void LogReadFallback(ILogger logger, string exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Shared cache write was skipped: {ExceptionType}")]
    private static partial void LogWriteSkipped(ILogger logger, string exceptionType);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Shared cache invalidation failed; the advanced generation keeps reads correct: {ExceptionType}")]
    private static partial void LogInvalidationFailed(ILogger logger, string exceptionType);
}
