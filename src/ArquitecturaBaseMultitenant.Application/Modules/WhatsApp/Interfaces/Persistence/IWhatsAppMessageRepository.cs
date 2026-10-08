using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
public interface IWhatsAppMessageRepository
{
    Task LockStreamAsync(string phoneNumberId, string wamid, CancellationToken ct);
    Task LockEventAsync(string phoneNumberId, string wamid, string eventType, CancellationToken ct);
    Task<WhatsAppMessage?> GetEventAsync(string phoneNumberId, string wamid, string eventType, CancellationToken ct);
    Task<Guid?> GetOutboxUserIdAsync(Guid outboxMessageId, CancellationToken ct);
    Task<IReadOnlyList<WhatsAppMessage>> ListDeliveryEventsAsync(string phoneNumberId, string wamid, CancellationToken ct);
    Task<WhatsAppInboundClaim?> ClaimNextAsync(DateTime nowUtc, TimeSpan leaseLifetime, int maxAttempts, CancellationToken ct);
    Task<WhatsAppInboundHint?> GetProcessingHintAsync(Guid messageId, CancellationToken ct);
    Task<WhatsAppMessage?> GetForUpdateAsync(Guid messageId, CancellationToken ct);
    Task<IReadOnlyList<WhatsAppMessage>> ListPendingAsync(DateTime nowUtc, int limit, CancellationToken ct);
    Task<IReadOnlyList<WhatsAppMessage>> ListExpiredTextAsync(DateTime beforeUtc, int limit, CancellationToken ct);
    Task<IReadOnlyList<WhatsAppMessage>> ListOriginalForUpdateAsync(Guid userId, Guid? methodId, CancellationToken ct);
    void Add(WhatsAppMessage message);
}
