using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>Contactos de la organización del claim. Exige la transacción del caso de uso.</summary>
public interface IWhatsAppCustomerRepository
{
    Task LockAsync(string waId, CancellationToken ct);
    Task<WhatsAppCustomer?> GetByWaIdAsync(string waId, CancellationToken ct);
    Task<WhatsAppCustomer?> GetAsync(Guid id, uint? expectedVersion, CancellationToken ct);
    void Add(WhatsAppCustomer customer);
}
