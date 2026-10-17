using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;

/// <summary>Carga el número de Plataforma antes de aceptar pedidos y lo relee periódicamente para ver los cambios de otras instancias.</summary>
internal sealed class WhatsAppPlatformChannelWorker(WhatsAppPlatformChannelState state, IOptions<WhatsAppOptions> options,
    TimeProvider clock, ILogger<WhatsAppPlatformChannelWorker> logger)
    : PeriodicWorker(TimeSpan.FromSeconds(options.Value.ChannelRefreshSeconds), clock, logger)
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await state.LoadAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override Task RunIterationAsync(CancellationToken ct) => state.RefreshAsync(ct);
}
