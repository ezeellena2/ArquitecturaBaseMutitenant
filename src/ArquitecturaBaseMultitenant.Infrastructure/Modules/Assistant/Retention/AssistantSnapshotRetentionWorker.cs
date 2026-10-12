using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using Microsoft.Extensions.DependencyInjection;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Retention;

/// <summary>Installed module maintenance continues with emergency, personal and organization feature gates disabled.</summary>
internal sealed partial class AssistantSnapshotRetentionWorker(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<AssistantSnapshotRetentionWorker> logger) : PeriodicWorker(TimeSpan.FromMinutes(5), clock, logger)
{
    internal async Task<int> RunOnceAsync(CancellationToken ct)
    {
        // A fixed cutoff makes the pass finite. Cursor advancement also skips busy rows so the oldest locked
        // page cannot starve the rest; every later pass starts at the beginning and retries those rows.
        var cutoffUtc = Clock.GetUtcNow().UtcDateTime;
        AssistantSnapshotRetentionCandidate? after = null;
        var total = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            IReadOnlyList<AssistantSnapshotRetentionCandidate> candidates;
            await using (var readScope = scopes.CreateAsyncScope())
            {
                candidates = await readScope.ServiceProvider.GetRequiredService<IAssistantSnapshotRetentionRepository>()
                    .ListExpiredAsync(cutoffUtc, ct, after);
            }
            foreach (var candidate in candidates)
            {
                ct.ThrowIfCancellationRequested();
                await using var scope = scopes.CreateAsyncScope();
                using var tenantScope = candidate.ScopeTenantId is { } tenantId
                    ? scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId) : null;
                var result = await scope.ServiceProvider.GetRequiredService<IAssistantSnapshotRetentionService>().PurgeExpiredAsync(candidate, ct);
                if (result.IsFailure) LogFailed(logger, result.Error.Code);
                else total += result.Value;
            }
            if (candidates.Count < 100) break;
            after = candidates[^1];
        }
        if (total != 0) LogCleared(logger, total);
        return total;
    }

    protected override async Task RunIterationAsync(CancellationToken ct) => await RunOnceAsync(ct);

    [LoggerMessage(Level = LogLevel.Information, Message = "Assistant retention cleared {Count} expired snapshots")]
    private static partial void LogCleared(ILogger logger, int count);
    [LoggerMessage(Level = LogLevel.Error, Message = "Assistant retention failed: {Failure}")]
    private static partial void LogFailed(ILogger logger, string failure);
}
