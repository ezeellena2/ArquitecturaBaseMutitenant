using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>
/// Los lugares exclusivos compartidos entre copias (ADR 0064 §4): <c>SET NX</c> con vencimiento y un valor propio de quien
/// lo toma. Mientras lo tiene, la copia lo renueva; al soltarlo lo borra solo si sigue siendo suyo. Si la copia se cae, el
/// lugar vence solo. Las claves llevan la aplicación y el ambiente y la etiqueta <c>{slot}</c>. Si Redis no responde al
/// tomarlo, el lugar es de la copia (ADR 0058).
/// </summary>
internal sealed partial class RedisExclusiveSlots(RedisConnection connection, IOptions<CacheOptions> options, LocalExclusiveSlots local,
    TimeProvider timeProvider, ILogger<RedisExclusiveSlots> logger) : IExclusiveSlots
{
    internal static readonly TimeSpan Expiry = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan RenewEvery = TimeSpan.FromSeconds(10);

    // KEYS: el lugar. ARGV: el valor de quien lo tomó y, al renovar, el vencimiento en milisegundos.
    private const string RenewScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('PEXPIRE', KEYS[1], ARGV[2]) end
        return 0
        """;
    private const string ReleaseScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('DEL', KEYS[1]) end
        return 0
        """;

    public async Task<IAsyncDisposable?> TryTakeAsync(string scope, Guid holderId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        cancellationToken.ThrowIfCancellationRequested();
        RedisKey key = options.Value.KeyPrefix + "{slot}:" + scope + ":" + holderId.ToString("N");
        RedisValue token = Guid.NewGuid().ToString("N");
        try
        {
            var database = (await connection.GetAsync()).GetDatabase();
            if (!await database.StringSetAsync(key, token, expiry: Expiry, when: When.NotExists)) return null;
            return new Slot(database, key, token, timeProvider, logger);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            LogFallback(logger, exception.GetType().Name);
            return await local.TryTakeAsync(scope, holderId, cancellationToken);
        }
    }

    private sealed class Slot : IAsyncDisposable
    {
        private readonly IDatabase _database;
        private readonly RedisKey _key;
        private readonly RedisValue _token;
        private readonly TimeProvider _clock;
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _renewal;
        private int _released;

        public Slot(IDatabase database, RedisKey key, RedisValue token, TimeProvider clock, ILogger logger)
        {
            (_database, _key, _token, _clock, _logger) = (database, key, token, clock, logger);
            using (ExecutionContext.SuppressFlow()) _renewal = Task.Run(RenewAsync, CancellationToken.None);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0) return;
            await _stop.CancelAsync();
            await _renewal;
            _stop.Dispose();
            try
            {
                await _database.ScriptEvaluateAsync(ReleaseScript, [_key], [_token]);
            }
            catch (Exception exception) when (exception is RedisException or TimeoutException)
            {
                // Sin Redis no se puede borrar: el lugar vence solo.
                LogReleaseFailed(_logger, exception.GetType().Name);
            }
        }

        private async Task RenewAsync()
        {
            try
            {
                while (true)
                {
                    await Task.Delay(RenewEvery, _clock, _stop.Token);
                    try
                    {
                        await _database.ScriptEvaluateAsync(RenewScript, [_key], [_token, (long)Expiry.TotalMilliseconds]);
                    }
                    catch (Exception exception) when (exception is RedisException or TimeoutException)
                    {
                        LogRenewalFailed(_logger, exception.GetType().Name);
                    }
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
                // Soltado: deja de renovar.
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Exclusive slot fell back to this copy: {ExceptionType}")]
    private static partial void LogFallback(ILogger logger, string exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Exclusive slot renewal failed: {ExceptionType}")]
    private static partial void LogRenewalFailed(ILogger logger, string exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Exclusive slot release failed: {ExceptionType}")]
    private static partial void LogReleaseFailed(ILogger logger, string exceptionType);
}
