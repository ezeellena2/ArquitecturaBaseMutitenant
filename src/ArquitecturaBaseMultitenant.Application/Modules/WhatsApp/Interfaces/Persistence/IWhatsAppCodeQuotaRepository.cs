using ArquitecturaBaseMultitenant.Domain.WhatsApp;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
public interface IWhatsAppCodeQuotaRepository
{
    Task LockAsync(string phoneNumberId, DateOnly dayUtc, CancellationToken ct);
    Task<WhatsAppCodeQuota?> GetAsync(string phoneNumberId, DateOnly dayUtc, CancellationToken ct);
    void Add(WhatsAppCodeQuota quota);
}
