using Microsoft.Extensions.Options;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Configuration;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

internal sealed class AssistantBudgetAlertWorker(IServiceScopeFactory scopes, TimeProvider clock, IOptions<AssistantOptions> options, ILogger<AssistantBudgetAlertWorker> logger) : PeriodicWorker(options.Value.BudgetAlertWorkerInterval, clock, logger)
{
    // Preserve the original delayed first pass.
    private bool first = true;
    protected override async Task RunIterationAsync(CancellationToken ct)
    {
        if (first) { first = false; return; }
        await using var scope = scopes.CreateAsyncScope();
        var pending = await scope.ServiceProvider.GetRequiredService<IAssistantBudgetAlertRepository>().ListPendingAsync(ct);
        foreach (var id in pending)
            await scope.ServiceProvider.GetRequiredService<IAssistantBudgetAlertService>().RetryAsync(id, ct);
    }

}
