using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;

/// <summary>
/// Cada hora trae de Meta el estado de las plantillas de todas las cuentas vigentes: cubre webhooks perdidos y las del
/// sistema creadas a mano en Meta. Es global, como los números; no entra a ninguna organización.
/// </summary>
internal sealed class WhatsAppTemplateSyncWorker(IServiceScopeFactory scopes, IOptions<WhatsAppOptions> options, TimeProvider clock,
    ILogger<WhatsAppTemplateSyncWorker> logger) : PeriodicWorker(TimeSpan.FromHours(1), clock, logger)
{
    /// <summary>Como la cola de entrada: lo que llega de Meta se procesa en segundo plano salvo que la instalación lo apague (las pruebas lo corren a mano).</summary>
    protected override bool Enabled => options.Value.ProcessInboundInBackground;

    internal async Task RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IWhatsAppTemplateSyncService>().SyncAsync(ct);
    }

    protected override Task RunIterationAsync(CancellationToken ct) => RunOnceAsync(ct);
}
