using ArquitecturaBaseMultitenant.Domain.Results;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
public interface IWhatsAppMessageRetentionService
{
    Task<Result<int>> ClearExpiredTextsAsync(CancellationToken ct);
}
