using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Retention;

/// <summary>La privacidad sigue con el canal apagado. Cada lote tiene un scope y una UoW propios.</summary>
internal sealed partial class WhatsAppMessageRetentionService(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<WhatsAppMessageRetentionService> logger) : BackgroundService
{
    internal async Task<int> RunOnceAsync(CancellationToken ct)
    {
        var total = 0;
        while (!ct.IsCancellationRequested)
        {
            await using var scope = scopes.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<IWhatsAppMessageRetentionService>().ClearExpiredTextsAsync(ct);
            if (result.IsFailure) { LogFailed(logger, result.Error.Code); break; }
            total += result.Value;
            if (result.Value == 0) break;
        }
        if (total != 0) LogCleared(logger, total);
        return total;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Scaffolding del contrato construye el host sin base; no es una instalación en ejecución.
        if (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name is "GetDocument.Insider") return;
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1), clock);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { LogFailed(logger, exception.GetType().Name); }
            try { if (!await timer.WaitForNextTickAsync(stoppingToken)) break; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
    [LoggerMessage(Level = LogLevel.Information, Message = "WhatsApp retention cleared {Count} texts after 90 days")]
    private static partial void LogCleared(ILogger logger, int count);
    [LoggerMessage(Level = LogLevel.Error, Message = "WhatsApp retention failed: {Failure}")]
    private static partial void LogFailed(ILogger logger, string failure);
}
