using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;

/// <summary>
/// El correo «Chats esperando» (D6) cada minuto: cada organización con un número vigente, dentro de su alcance, y después
/// los equipos de operadores de Plataforma. Cada vuelta tiene su scope y su UoW.
/// </summary>
internal sealed class WhatsAppWaitingNoticeWorker(IServiceScopeFactory scopes, TenantJobRunner tenants, TimeProvider clock,
    ILogger<WhatsAppWaitingNoticeWorker> logger) : PeriodicWorker(TimeSpan.FromMinutes(1), clock, logger)
{
    internal async Task RunOnceAsync(CancellationToken ct)
    {
        IReadOnlyList<Guid> organizations;
        await using (var listing = scopes.CreateAsyncScope())
            organizations = await listing.ServiceProvider.GetRequiredService<IWhatsAppChannelReader>().ListOrganizationIdsAsync(ct);
        await tenants.RunAsync(organizations, (provider, token) =>
            provider.GetRequiredService<IWhatsAppWaitingNoticeService>().NotifyOrganizationAsync(token), ct);
        await using var platform = scopes.CreateAsyncScope();
        await platform.ServiceProvider.GetRequiredService<IWhatsAppWaitingNoticeService>().NotifyPlatformAsync(ct);
    }

    protected override Task RunIterationAsync(CancellationToken ct) => RunOnceAsync(ct);
}
