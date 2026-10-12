using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Files;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Retention;

internal sealed partial class AssistantRetentionWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<AssistantRetentionWorker> logger) : PeriodicWorker(TimeSpan.FromMinutes(5), clock, logger)
{
    internal async Task<int> RunOnceAsync(CancellationToken ct)
    {
        IReadOnlyList<Guid> tenantIds;
        int retentionDays;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IAssistantRetentionRepository>();
            tenantIds = await repository.ListTenantIdsAsync(ct);
            retentionDays = await repository.RetentionDaysAsync(ct);
        }
        var cutoffUtc = Clock.GetUtcNow().UtcDateTime.AddDays(-retentionDays);
        var total = 0;
        foreach (var tenantId in tenantIds.Select(value => (Guid?)value).Prepend(null))
        {
            await using var scope = scopes.CreateAsyncScope();
            var services = scope.ServiceProvider;
            using var tenantScope = tenantId is { } id ? services.GetRequiredService<ITenantScope>().Enter(id) : null;
            var repository = services.GetRequiredService<IAssistantRetentionRepository>();
            var service = services.GetRequiredService<IAssistantRetentionService>();
            Guid? after = null;
            while (true)
            {
                var rows = await repository.ListExpiredAsync(cutoffUtc, after, ct);
                foreach (var row in rows)
                {
                    var purged = await service.PurgeAsync(row, cutoffUtc, ct);
                    if (purged.IsSuccess) total += purged.Value;
                }
                if (rows.Count < 100) break;
                after = rows[^1].Id;
            }
            // Physical deletion is idempotent and outside UoW. Failed keys remain durable for the next pass.
            foreach (var row in await repository.ListCleanupAsync(ct))
            {
                try
                {
                    await services.GetRequiredService<IFileStore>().DeleteAsync(row.StorageKey, ct);
                    await service.CompleteCleanupAsync(row.Id, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception exception) { LogFailure(logger, exception.GetType().Name); }
            }
        }
        // A durable filesystem journal closes crashes between the atomic store write and database commit.
        // The 24-hour grace excludes any live upload. Recheck every real RLS scope before deleting a key.
        await using (var cleanupScope = scopes.CreateAsyncScope())
        {
            var maintenance = cleanupScope.ServiceProvider.GetRequiredService<IFileStoreMaintenance>();
            var files = cleanupScope.ServiceProvider.GetRequiredService<IFileStore>();
            foreach (var key in await maintenance.ListUnconfirmedAsync("assistant", Clock.GetUtcNow().UtcDateTime.AddHours(-24), ct))
            {
                var referenced = false;
                foreach (var tenantId in tenantIds.Select(value => (Guid?)value).Prepend(null))
                {
                    await using var readScope = scopes.CreateAsyncScope();
                    using var tenantScope = tenantId is { } id ? readScope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(id) : null;
                    if (await readScope.ServiceProvider.GetRequiredService<IAssistantRetentionRepository>().IsStorageReferencedAsync(key, ct))
                    {
                        referenced = true;
                        break;
                    }
                }
                if (referenced) await maintenance.ConfirmAsync(key, "assistant", ct);
                else await files.DeleteAsync(key, ct);
            }
        }
        return total;
    }

    protected override async Task RunIterationAsync(CancellationToken ct) => await RunOnceAsync(ct);

    [LoggerMessage(Level = LogLevel.Error, Message = "Assistant cleanup failed: {Failure}")]
    private static partial void LogFailure(ILogger logger, string failure);
}
