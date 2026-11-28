using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;

public interface IWhatsAppWebhookService
{
    Task<Result<string>> VerifyAsync(string? mode, string? verifyToken, string? challenge, CancellationToken ct);
    Task<Result> ReceiveAsync(WhatsAppWebhookRequest request, CancellationToken ct);
}
