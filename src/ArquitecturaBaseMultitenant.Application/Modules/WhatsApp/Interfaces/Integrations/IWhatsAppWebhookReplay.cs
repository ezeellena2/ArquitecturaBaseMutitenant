using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
/// <summary>Reabre el mismo caso de uso en un scope fresco después de un rollback por duplicado concurrente o para aplicar fallos por organización.</summary>
public interface IWhatsAppWebhookReplay
{
    Task<Result> ReplayAsync(WhatsAppWebhookRequest request, CancellationToken ct);
    Task ApplyDeliveryFailuresAsync(IReadOnlyList<WhatsAppDeliveryFailure> failures, CancellationToken ct);
}
