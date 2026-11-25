using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using Microsoft.Extensions.DependencyInjection;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging;

/// <summary>Busca periódicamente avisos pendientes y entrega cada uno mediante el servicio de Application. Cada aviso tiene su propio scope y commit para aislar reintentos.</summary>
internal sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<OutboxOptions> options,
    IEnumerable<IKeyRingReadiness> keyRings,
    ILogger<OutboxDispatcher> logger) : PeriodicWorker(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds), timeProvider, logger)
{
    protected override async Task RunIterationAsync(CancellationToken ct)
    {
        // 0064 §11: una copia con otro anillo no abre lo que guardaron las demás ni escribe lo que ellas no podrían abrir.
        foreach (var keyRing in keyRings) if (!await keyRing.MatchesAsync(ct)) return;
        // El transporte SMTP vive durante el lote; cada mensaje conserva su propia UoW.
        await using var senderScope = scopeFactory.CreateAsyncScope();
        var senders = senderScope.ServiceProvider.GetServices<IChannelSender>().ToArray();
        var processor = senderScope.ServiceProvider.GetRequiredService<OutboxDeliveryProcessor>();
        for (var count = 0; count < options.Value.BatchSize && senders.Length > 0 && !ct.IsCancellationRequested; count++)
            if (await processor.ProcessOnceAsync(senders, ct) == 0) break;
    }
}
