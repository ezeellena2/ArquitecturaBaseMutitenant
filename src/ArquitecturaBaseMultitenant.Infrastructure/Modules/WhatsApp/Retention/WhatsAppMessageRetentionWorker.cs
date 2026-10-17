using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Logging;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Retention;

/// <summary>La privacidad sigue con el canal apagado. Cada lote tiene un scope y una UoW propios.</summary>
internal sealed partial class WhatsAppMessageRetentionWorker(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<WhatsAppMessageRetentionWorker> logger) : PeriodicWorker(TimeSpan.FromDays(1), clock, logger)
{
    internal async Task<int> RunOnceAsync(CancellationToken ct)
    {
        var total = 0;
        while (!ct.IsCancellationRequested)
        {
            await using var scope = scopes.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<IWhatsAppMessageRetentionService>().ClearExpiredTextsAsync(ct);
            if (result.IsFailure) { LogFailed(Logger, result.Error.Code); break; }
            total += result.Value;
            if (result.Value == 0) break;
        }
        if (total != 0) LogCleared(Logger, total);
        return total;
    }
    protected override Task RunIterationAsync(CancellationToken ct) => RunOnceAsync(ct);
    [LoggerMessage(Level = LogLevel.Information, Message = "WhatsApp retention cleared {Count} expired items")]
    private static partial void LogCleared(ILogger logger, int count);
    [LoggerMessage(Level = LogLevel.Error, Message = "WhatsApp retention failed: {Failure}")]
    private static partial void LogFailed(ILogger logger, string failure);
}
