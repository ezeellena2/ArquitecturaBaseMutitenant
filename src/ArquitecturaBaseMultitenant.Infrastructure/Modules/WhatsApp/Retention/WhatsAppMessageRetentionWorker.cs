using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.DependencyInjection;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Logging;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Retention;

/// <summary>
/// La privacidad sigue con el canal apagado. Una vez por día vacía la copia global de la entrada vencida y el contenido vencido de
/// los chats (272): los de cada organización, también de una suspendida o cerrada, uno por vez en su propio alcance con
/// <see cref="TenantJobRunner"/> (sin un rol que saltee el aislamiento), y los del número de Plataforma. Cada lote va en su
/// transacción y toma solo filas libres (<c>SKIP LOCKED</c>), así otra copia que corre a la vez no repite ni pisa; una
/// organización que falla no frena a las demás.
/// </summary>
internal sealed partial class WhatsAppMessageRetentionWorker(IServiceScopeFactory scopes, TenantJobRunner runner, TimeProvider clock,
    ILogger<WhatsAppMessageRetentionWorker> logger) : PeriodicWorker(TimeSpan.FromDays(1), clock, logger)
{
    internal async Task<int> RunOnceAsync(CancellationToken ct)
    {
        var total = 0;
        while (!ct.IsCancellationRequested)
        {
            await using var scope = scopes.CreateAsyncScope();
            var cleared = await BatchAsync(scope.ServiceProvider.GetRequiredService<IWhatsAppMessageRetentionService>().ClearExpiredTextsAsync(ct));
            if (cleared is not > 0) break;
            total += cleared.Value;
        }
        while (!ct.IsCancellationRequested)
        {
            await using var scope = scopes.CreateAsyncScope();
            var cleared = await BatchAsync(scope.ServiceProvider.GetRequiredService<IWhatsAppMessageRetentionService>().ClearExpiredPlatformChatsAsync(ct));
            if (cleared is not > 0) break;
            total += cleared.Value;
        }
        var organizations = 0;
        await runner.RunBusinessesAsync(async (services, token) => organizations += await PurgeOrganizationAsync(services, token), ct);
        total += organizations;
        if (total != 0) LogCleared(Logger, total);
        return total;
    }

    /// <summary>Los chats de la organización del scope, lote por lote hasta que no queda nada vencido.</summary>
    internal async Task<int> PurgeOrganizationAsync(IServiceProvider services, CancellationToken ct)
    {
        var total = 0;
        try
        {
            var retention = services.GetRequiredService<IWhatsAppMessageRetentionService>();
            while (!ct.IsCancellationRequested && await BatchAsync(retention.ClearExpiredOrganizationChatsAsync(ct)) is > 0 and var cleared)
                total += cleared;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception exception) { ExceptionLog.Write(Logger, nameof(WhatsAppMessageRetentionWorker), exception); }
        return total;
    }

    /// <summary>Lo que borró el lote, o null si falló (queda en el log con su código).</summary>
    private async Task<int?> BatchAsync(Task<Result<int>> batch)
    {
        var result = await batch;
        if (result.IsSuccess) return result.Value;
        LogFailed(Logger, result.Error.Code);
        return null;
    }

    protected override Task RunIterationAsync(CancellationToken ct) => RunOnceAsync(ct);
    [LoggerMessage(Level = LogLevel.Information, Message = "WhatsApp retention cleared {Count} expired items")]
    private static partial void LogCleared(ILogger logger, int count);
    [LoggerMessage(Level = LogLevel.Error, Message = "WhatsApp retention failed: {Failure}")]
    private static partial void LogFailed(ILogger logger, string failure);
}
