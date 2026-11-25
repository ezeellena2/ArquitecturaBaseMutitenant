using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
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
internal sealed class WhatsAppTemplateSyncWorker(IServiceScopeFactory scopes, IOptions<WhatsAppOptions> options,
    IEnumerable<IKeyRingReadiness> keyRings, TimeProvider clock,
    ILogger<WhatsAppTemplateSyncWorker> logger) : PeriodicWorker(TimeSpan.FromHours(1), clock, logger)
{
    /// <summary>Como la cola de entrada: lo que llega de Meta se procesa en segundo plano salvo que la instalación lo apague (las pruebas lo corren a mano).</summary>
    protected override bool Enabled => options.Value.ProcessInboundInBackground;

    internal async Task RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IWhatsAppTemplateSyncService>().SyncAsync(ct);
    }

    protected override async Task RunIterationAsync(CancellationToken ct)
    {
        // 0064 §11: una copia con otro anillo no abre lo que guardaron las demás ni escribe lo que ellas no podrían abrir.
        foreach (var keyRing in keyRings) if (!await keyRing.MatchesAsync(ct)) return;
        await RunOnceAsync(ct);
    }
}
