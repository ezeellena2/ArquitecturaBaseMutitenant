using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Configuration;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using Microsoft.Extensions.DependencyInjection;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Retention;

/// <summary>Installed module maintenance continues with emergency, personal and organization feature gates disabled.</summary>
internal sealed partial class AssistantSnapshotRetentionWorker(IServiceScopeFactory scopes, TenantJobRunner jobs, TimeProvider clock,
    IOptions<AssistantOptions> options, ILogger<AssistantSnapshotRetentionWorker> logger) : PeriodicWorker(options.Value.SnapshotRetentionWorkerInterval, clock, logger)
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
                if (candidate.ScopeTenantId is { } tenantId) await jobs.RunAsync([tenantId], PurgeAsync, ct);
                else
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await PurgeAsync(scope.ServiceProvider, ct);
                }
                async Task PurgeAsync(IServiceProvider services, CancellationToken token)
                {
                    var result = await services.GetRequiredService<IAssistantSnapshotRetentionService>().PurgeExpiredAsync(candidate, token);
                    if (result.IsFailure) LogFailed(logger, result.Error.Code);
                    else total += result.Value;
                }
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
