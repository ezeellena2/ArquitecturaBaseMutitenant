using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Configuration.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Settings;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>
/// Admite cada pedido en sus cuotas compartidas (E9), con las que guardó Plataforma o las de arranque mientras no hay
/// configuración. Cuenta en Redis; si no responde, en PostgreSQL con su propia unidad de trabajo, antes del negocio y sin
/// anidar su transacción, sembrada con lo último que esta instancia vio en Redis para que el consumo siga.
/// </summary>
internal sealed class RateLimitService(IPlatformSettingsReader settings, IOptions<RateLimitOptions> defaults,
    IRateLimitCounter counter, IRateLimitIpHasher ipHasher, IRateLimitCounterRepository counters, IUnitOfWork unitOfWork,
    TimeProvider timeProvider, ILogger<RateLimitService> logger) : IRateLimitService
{
    internal const int CleanupBatchSize = 1000;

    public Task<Result> AdmitAsync(RateLimitRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "AdmitRateLimit", async () =>
        {
            if (request.Profiles.Count == 0) return Result.Success();
            var limits = await settings.FindRateLimitsAsync(cancellationToken) ?? defaults.Value.ToLimits();
            var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
            RateLimitBucket[] buckets =
            [
                .. request.Profiles.Distinct().Select(profile =>
                {
                    var quota = limits.For(profile);
                    return new RateLimitBucket(profile, Partition(profile, request), quota, RateLimitWindow.Of(nowUtc, quota));
                }),
            ];
            var tally = await counter.TryAdmitAsync(buckets, cancellationToken)
                ?? (await unitOfWork.ExecuteInTransactionAsync(ct => AdmitDurablyAsync(buckets, ct), CommitPolicy.OnSuccess,
                    cancellationToken)).Value;
            return tally.Admitted ? Result.Success() : RateLimitErrors.Exceeded(RetryAfter(buckets, tally, nowUtc));
        });
    }

    public Task<Result> CleanupExpiredAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "CleanupRateLimitCounters", () =>
            unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await counters.DeleteExpiredAsync(timeProvider.GetUtcNow().UtcDateTime, CleanupBatchSize, ct);
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken));

    // Locks → lecturas → regla → escrituras: si alguna cuota no tiene lugar, no suma en ninguna.
    private async Task<Result<RateLimitTally>> AdmitDurablyAsync(RateLimitBucket[] buckets, CancellationToken cancellationToken)
    {
        var counts = await counters.LockAsync(buckets, counter.LastSeen(buckets), cancellationToken);
        if (buckets.Where((bucket, index) => counts[index] >= bucket.Quota.PermitLimit).Any())
            return new RateLimitTally(false, counts);
        await counters.IncrementAsync(buckets, cancellationToken);
        return new RateLimitTally(true, [.. counts.Select(count => count + 1)]);
    }

    private string Partition(RateLimitProfile profile, RateLimitRequest request) => RateLimitProfiles.PartitionOf(profile) switch
    {
        RateLimitPartitionKind.Identity => (request.UserId
            ?? throw new ArgumentException("An identity quota needs the signed identity.", nameof(request)))
            .ToString("N", CultureInfo.InvariantCulture),
        RateLimitPartitionKind.Tenant => (request.TenantId
            ?? throw new ArgumentException("An organization quota needs the organization of the claim.", nameof(request)))
            .ToString("N", CultureInfo.InvariantCulture),
        _ => ipHasher.Hash(request.IpAddress),
    };

    // Para entrar hay que esperar a que se renueven todas las cuotas llenas: la más lejana manda.
    private static int RetryAfter(RateLimitBucket[] buckets, RateLimitTally tally, DateTime nowUtc) =>
        buckets.Where((bucket, index) => tally.Counts[index] >= bucket.Quota.PermitLimit)
            .Select(bucket => bucket.Window.SecondsLeft(nowUtc))
            .DefaultIfEmpty(1)
            .Max();
}
