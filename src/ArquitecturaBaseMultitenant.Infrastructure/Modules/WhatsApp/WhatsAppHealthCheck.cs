using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
/// <summary>Readiness local: consulta metadatos de cola, jamás envía un mensaje a Meta.</summary>
internal sealed class WhatsAppHealthCheck(IWhatsAppAvailability availability, IServiceScopeFactory scopes) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!availability.IsEnabled) return HealthCheckResult.Healthy("WhatsApp disabled.");
        using var scope = scopes.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var pending = await database.OutboxMessages.AsNoTracking().CountAsync(value => value.Channel == "whatsapp"
            && value.Status == OutboxStatus.Pending, cancellationToken);
        return HealthCheckResult.Healthy("WhatsApp configured.", new Dictionary<string, object> { ["pending"] = pending });
    }
}
