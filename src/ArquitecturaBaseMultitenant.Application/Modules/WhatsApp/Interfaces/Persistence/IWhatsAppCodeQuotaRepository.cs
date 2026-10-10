using ArquitecturaBaseMultitenant.Domain.WhatsApp;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
public interface IWhatsAppCodeQuotaRepository
{
    Task LockAsync(string phoneNumberId, DateOnly dayUtc, CancellationToken ct);
    Task<WhatsAppCodeQuota?> GetAsync(string phoneNumberId, DateOnly dayUtc, CancellationToken ct);
    void Add(WhatsAppCodeQuota quota);
    Task<WhatsAppCodeQuotaIp?> GetIpAsync(Guid quotaId, string ipKey, CancellationToken ct);
    void Add(WhatsAppCodeQuotaIp quota);
    Task<IReadOnlyList<WhatsAppCodeQuotaIp>> ListExpiredIpsAsync(DateOnly todayUtc, int count, CancellationToken ct);
    void Remove(WhatsAppCodeQuotaIp quota);
}
