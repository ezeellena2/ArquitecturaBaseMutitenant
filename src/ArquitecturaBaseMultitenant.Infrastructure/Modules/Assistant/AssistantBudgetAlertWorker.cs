using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

internal sealed partial class AssistantBudgetAlertWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<AssistantBudgetAlertWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name is "GetDocument.Insider") return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var pending = await scope.ServiceProvider.GetRequiredService<IAssistantBudgetAlertRepository>().ListPendingAsync(stoppingToken);
                foreach (var id in pending)
                    await scope.ServiceProvider.GetRequiredService<IAssistantBudgetAlertService>().RetryAsync(id, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { LogFailure(logger, exception.GetType().Name); }
        }
    }
    [LoggerMessage(Level = LogLevel.Warning, Message = "Assistant budget alert retry failed: {Failure}")]
    private static partial void LogFailure(ILogger logger, string failure);
}
