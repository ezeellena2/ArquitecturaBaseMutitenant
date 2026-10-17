using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;

/// <summary>
/// Cada minuto manda las difusiones que ya tienen que salir, en tandas, dentro del alcance de cada organización con un
/// número vigente. Como la cola de entrada, se apaga cuando la instalación no procesa en segundo plano (las pruebas lo
/// corren a mano).
/// </summary>
internal sealed class WhatsAppBroadcastWorker(IServiceScopeFactory scopes, TenantJobRunner tenants, IOptions<WhatsAppOptions> options, TimeProvider clock,
    ILogger<WhatsAppBroadcastWorker> logger) : PeriodicWorker(TimeSpan.FromMinutes(1), clock, logger)
{
    /// <summary>Tandas por organización y vuelta: el resto sale en la vuelta siguiente.</summary>
    private const int MaxBatchesPerRun = 20;

    protected override bool Enabled => options.Value.ProcessInboundInBackground;

    internal async Task RunOnceAsync(CancellationToken ct)
    {
        IReadOnlyList<Guid> organizations;
        await using (var listing = scopes.CreateAsyncScope())
            organizations = await listing.ServiceProvider.GetRequiredService<IWhatsAppChannelReader>().ListOrganizationIdsAsync(ct);
        await tenants.RunAsync(organizations, async (provider, token) =>
        {
            var dispatch = provider.GetRequiredService<IWhatsAppBroadcastDispatchService>();
            for (var batch = 0; batch < MaxBatchesPerRun; batch++)
                if ((await dispatch.DispatchNextAsync(token)) is not { IsSuccess: true, Value: true }) break;
        }, ct);
    }

    protected override Task RunIterationAsync(CancellationToken ct) => RunOnceAsync(ct);
}
