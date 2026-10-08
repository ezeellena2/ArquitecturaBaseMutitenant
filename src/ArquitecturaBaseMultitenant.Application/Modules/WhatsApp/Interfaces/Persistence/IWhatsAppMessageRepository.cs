using ArquitecturaBaseMultitenant.Domain.WhatsApp;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
public interface IWhatsAppMessageRepository
{
    Task LockEventAsync(string phoneNumberId, string wamid, string eventType, CancellationToken ct);
    Task<WhatsAppMessage?> GetEventAsync(string phoneNumberId, string wamid, string eventType, CancellationToken ct);
    Task<IReadOnlyList<WhatsAppMessage>> ListPendingAsync(DateTime nowUtc, int limit, CancellationToken ct);
    Task<IReadOnlyList<WhatsAppMessage>> ListExpiredTextAsync(DateTime beforeUtc, int limit, CancellationToken ct);
    void Add(WhatsAppMessage message);
}
