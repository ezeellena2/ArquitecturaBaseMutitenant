using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Files;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Configuration;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Retention;

internal sealed class AssistantRetentionWorker(IServiceScopeFactory scopes, TenantJobRunner jobs, TimeProvider clock,
    IOptions<AssistantOptions> options, ILogger<AssistantRetentionWorker> logger) : PeriodicWorker(options.Value.RetentionWorkerInterval, clock, logger)
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
        await using (var global = scopes.CreateAsyncScope()) await PurgeScopeAsync(global.ServiceProvider, ct);
        await jobs.RunAsync(tenantIds, PurgeScopeAsync, ct);
        async Task PurgeScopeAsync(IServiceProvider services, CancellationToken token)
        {
            var repository = services.GetRequiredService<IAssistantRetentionRepository>();
            var service = services.GetRequiredService<IAssistantRetentionService>();
            Guid? after = null;
            while (true)
            {
                var rows = await repository.ListExpiredAsync(cutoffUtc, after, token);
                foreach (var row in rows)
                {
                    var purged = await service.PurgeAsync(row, cutoffUtc, token);
                    if (purged.IsSuccess) total += purged.Value;
                }
                if (rows.Count < 100) break;
                after = rows[^1].Id;
            }
            // Physical deletion is idempotent and outside UoW. Failed keys remain durable for the next pass.
            foreach (var row in await repository.ListCleanupAsync(token))
            {
                try
                {
                    await services.GetRequiredService<IFileStore>().DeleteAsync(row.StorageKey, token);
                    await service.CompleteCleanupAsync(row.Id, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception exception) { ExceptionLog.Write(logger, "AssistantFileCleanup", exception); }
            }
        }
        // A durable filesystem journal closes crashes between the atomic store write and database commit.
        // The 24-hour grace excludes any live upload. Recheck every real RLS scope before deleting a key.
        await using (var cleanupScope = scopes.CreateAsyncScope())
        {
            var maintenance = cleanupScope.ServiceProvider.GetRequiredService<IFileStoreMaintenance>();
            var files = cleanupScope.ServiceProvider.GetRequiredService<IFileStore>();
            foreach (var key in await maintenance.ListUnconfirmedAsync("assistant", Clock.GetUtcNow().UtcDateTime.Subtract(options.Value.UnconfirmedFileGrace), ct))
            {
                var referenced = false;
                await using (var global = scopes.CreateAsyncScope()) await CheckScopeAsync(global.ServiceProvider, ct);
                if (!referenced) await jobs.RunAsync(tenantIds, CheckScopeAsync, ct);
                async Task CheckScopeAsync(IServiceProvider services, CancellationToken token)
                {
                    if (!referenced) referenced = await services.GetRequiredService<IAssistantRetentionRepository>().IsStorageReferencedAsync(key, token);
                }
                if (referenced) await maintenance.ConfirmAsync(key, "assistant", ct);
                else await files.DeleteAsync(key, ct);
            }
        }
        return total;
    }

    protected override async Task RunIterationAsync(CancellationToken ct) => await RunOnceAsync(ct);

}
