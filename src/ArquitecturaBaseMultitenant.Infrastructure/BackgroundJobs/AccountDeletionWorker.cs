using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;

/// <summary>Un scope por reclamo, organización y cierre. El lease vence si se interrumpe el proceso.</summary>
internal sealed partial class AccountDeletionWorker(IServiceScopeFactory scopes, TimeProvider timeProvider,
    ILogger<AccountDeletionWorker> logger) : BackgroundService
{
    internal async Task RunOnceAsync(CancellationToken ct)
    {
        for (var count = 0; count < 100 && !ct.IsCancellationRequested; count++)
        {
            await using var claimScope = scopes.CreateAsyncScope();
            var claimed = await claimScope.ServiceProvider.GetRequiredService<IAccountDeletionProcessingService>().ClaimNextAsync(ct);
            if (claimed.IsFailure || claimed.Value is not { } work) break;
            try
            {
                var succeeded = true;
                foreach (var tenant in work.Tenants)
                {
                    await using var tenantScope = scopes.CreateAsyncScope();
                    var result = await tenantScope.ServiceProvider.GetRequiredService<IAccountDeletionProcessingService>()
                        .ProcessTenantAsync(work, tenant, ct);
                    if (result.IsFailure) { succeeded = false; break; }
                }
                if (!succeeded) continue;
                await using var finalScope = scopes.CreateAsyncScope();
                var completed = await finalScope.ServiceProvider.GetRequiredService<IAccountDeletionProcessingService>().CompleteAsync(work, ct);
                if (completed.IsFailure) LogFailed(logger, completed.Error.Code);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception exception) { LogFailed(logger, exception.GetType().Name); }
        }
        await ProcessRetentionsAsync(ct);
    }

    private async Task ProcessRetentionsAsync(CancellationToken ct)
    {
        await using var readScope = scopes.CreateAsyncScope();
        var pending = await readScope.ServiceProvider.GetRequiredService<IAccountDeletionProcessingService>()
            .ListExpiredRetentionsAsync(ct);
        if (pending.IsFailure) { LogFailed(logger, pending.Error.Code); return; }
        foreach (var work in pending.Value)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await using var processingScope = scopes.CreateAsyncScope();
                var result = await processingScope.ServiceProvider.GetRequiredService<IAccountDeletionProcessingService>()
                    .ProcessRetentionAsync(work, ct);
                if (result.IsFailure) LogFailed(logger, result.Error.Code);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception exception) { LogFailed(logger, exception.GetType().Name); }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), timeProvider);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
                if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                LogFailed(logger, exception.GetType().Name);
                try { if (!await timer.WaitForNextTickAsync(stoppingToken)) break; }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Account deletion processing failed: {Failure}")]
    private static partial void LogFailed(ILogger logger, string failure);
}
