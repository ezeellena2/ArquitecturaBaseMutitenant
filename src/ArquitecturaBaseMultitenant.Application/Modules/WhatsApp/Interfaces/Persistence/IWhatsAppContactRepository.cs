using ArquitecturaBaseMultitenant.Domain.WhatsApp;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
public interface IWhatsAppContactRepository
{
    Task LockAsync(string waId, CancellationToken ct);
    Task<WhatsAppContact?> GetAsync(string waId, CancellationToken ct);
    Task<IReadOnlyList<WhatsAppContact>> ListByMethodAsync(Guid methodId, CancellationToken ct);
    void Add(WhatsAppContact contact);
    void Remove(WhatsAppContact contact);
}
