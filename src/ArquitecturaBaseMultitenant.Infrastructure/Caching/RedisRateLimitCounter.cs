using System.Collections.Concurrent;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Models.Settings;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>
/// El contador compartido de cuotas en Redis (E9, ADR 0052). Un script admite el pedido en todas sus cuotas a la vez y sólo
/// suma si todas tienen lugar. Las claves llevan la aplicación y el ambiente, la etiqueta <c>{rl}</c> (un Redis con cluster
/// las ubica juntas) y vencen después de su ventana. Recuerda lo último que vio de cada cuota para sembrar el respaldo de
/// PostgreSQL si Redis cae; al volver, mientras duren las ventanas de la caída, sube cada cuota a lo que contó PostgreSQL.
/// </summary>
internal sealed partial class RedisRateLimitCounter(RedisConnection connection, IOptions<CacheOptions> options,
    IServiceScopeFactory scopes, CacheMetrics metrics, TimeProvider timeProvider, ILogger<RedisRateLimitCounter> logger)
    : IRateLimitCounter
{
    private const int MaxRemembered = 50_000;
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan PruneEvery = TimeSpan.FromSeconds(10);

    // KEYS: las cuotas. ARGV, de a tres por cuota: tope, vencimiento en milisegundos Unix y piso.
    private const string AdmitScript = """
        local admitted = 1
        local counts = {}
        for i = 1, #KEYS do
          local current = tonumber(redis.call('GET', KEYS[i]) or '0')
          local floor = tonumber(ARGV[3 * i])
          if floor > current then
            redis.call('SET', KEYS[i], floor, 'PXAT', ARGV[3 * i - 1])
            current = floor
          end
          counts[i] = current
          if current >= tonumber(ARGV[3 * i - 2]) then admitted = 0 end
        end
        if admitted == 1 then
          for i = 1, #KEYS do
            counts[i] = redis.call('INCR', KEYS[i])
            redis.call('PEXPIREAT', KEYS[i], ARGV[3 * i - 1])
          end
        end
        table.insert(counts, 1, admitted)
        return counts
        """;

    private readonly ConcurrentDictionary<string, Seen> _seen = new(StringComparer.Ordinal);
    private long _recoveringUntilTicks;
    private long _prunedAtTicks;

    public async Task<RateLimitTally?> TryAdmitAsync(IReadOnlyList<RateLimitBucket> buckets,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buckets);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        try
        {
            var floors = nowUtc.Ticks < Interlocked.Read(ref _recoveringUntilTicks)
                ? await DurableCountsAsync(buckets, cancellationToken)
                : new int[buckets.Count];
            var redis = (await connection.GetAsync()).GetDatabase();
            var keys = buckets.Select(bucket => (RedisKey)(options.Value.KeyPrefix + "{rl}:" + bucket.Key)).ToArray();
            var values = buckets.SelectMany((bucket, index) => new RedisValue[]
            {
                bucket.Quota.PermitLimit,
                new DateTimeOffset(bucket.Window.EndUtc + Grace).ToUnixTimeMilliseconds(),
                floors[index],
            }).ToArray();
            var result = (RedisResult[])(await redis.ScriptEvaluateAsync(AdmitScript, keys, values))!;
            int[] counts = [.. result.Skip(1).Select(count => (int)count)];
            Remember(buckets, counts, nowUtc);
            return new RateLimitTally((int)result[0] == 1, counts);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            // Mientras duren las ventanas de esta caída, cada admisión que vuelva a Redis lee primero lo que contó PostgreSQL.
            var until = buckets.Max(bucket => bucket.Window.EndUtc).Ticks;
            for (var current = Interlocked.Read(ref _recoveringUntilTicks); current < until;
                 current = Interlocked.Read(ref _recoveringUntilTicks))
            {
                if (Interlocked.CompareExchange(ref _recoveringUntilTicks, until, current) == current) break;
            }

            metrics.RateLimitFallback();
            LogFallback(logger, exception.GetType().Name);
            return null;
        }
    }

    public IReadOnlyList<int> LastSeen(IReadOnlyList<RateLimitBucket> buckets)
    {
        ArgumentNullException.ThrowIfNull(buckets);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        return [.. buckets.Select(bucket =>
            _seen.TryGetValue(bucket.Key, out var seen) && seen.ExpiresAtUtc > nowUtc ? seen.Count : 0)];
    }

    private async Task<int[]> DurableCountsAsync(IReadOnlyList<RateLimitBucket> buckets, CancellationToken cancellationToken)
    {
        string[] keys = [.. buckets.Select(bucket => bucket.Key)];
        await using var scope = scopes.CreateAsyncScope();
        var counted = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().RateLimitCounters
            .AsNoTracking()
            .Where(counter => keys.Contains(counter.Key))
            .ToDictionaryAsync(counter => counter.Key, counter => counter.Count, StringComparer.Ordinal, cancellationToken);
        return [.. keys.Select(key => counted.GetValueOrDefault(key))];
    }

    private void Remember(IReadOnlyList<RateLimitBucket> buckets, int[] counts, DateTime nowUtc)
    {
        if (_seen.Count >= MaxRemembered && nowUtc.Ticks - Interlocked.Read(ref _prunedAtTicks) > PruneEvery.Ticks)
        {
            Interlocked.Exchange(ref _prunedAtTicks, nowUtc.Ticks);
            foreach (var expired in _seen.Where(entry => entry.Value.ExpiresAtUtc <= nowUtc))
                _seen.TryRemove(expired.Key, out _);
        }

        for (var index = 0; index < buckets.Count; index++)
        {
            if (_seen.Count >= MaxRemembered) return;
            var seen = new Seen(counts[index], buckets[index].Window.EndUtc);
            _seen.AddOrUpdate(buckets[index].Key, seen, (_, previous) => previous.Count > seen.Count ? previous : seen);
        }
    }

    private readonly record struct Seen(int Count, DateTime ExpiresAtUtc);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rate limit admission fell back to the database: {ExceptionType}")]
    private static partial void LogFallback(ILogger logger, string exceptionType);
}

/// <summary>Sin Redis configurado (el exportador OpenAPI y los proveedores técnicos), cada admisión cuenta en PostgreSQL.</summary>
internal sealed class DurableOnlyRateLimitCounter : IRateLimitCounter
{
    public Task<RateLimitTally?> TryAdmitAsync(IReadOnlyList<RateLimitBucket> buckets, CancellationToken cancellationToken) =>
        Task.FromResult<RateLimitTally?>(null);

    public IReadOnlyList<int> LastSeen(IReadOnlyList<RateLimitBucket> buckets) =>
        [.. buckets.Select(_ => 0)];
}
