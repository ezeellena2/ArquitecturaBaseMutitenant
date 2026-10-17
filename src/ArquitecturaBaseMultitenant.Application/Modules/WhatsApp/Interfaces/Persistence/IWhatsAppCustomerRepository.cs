using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>Contactos de la organización del claim. Exige la transacción del caso de uso.</summary>
public interface IWhatsAppCustomerRepository
{
    /// <summary>Serializa el alta de contactos de la organización: la entrada y la importación no crean el mismo número dos veces.</summary>
    Task LockAsync(CancellationToken ct);
    Task<WhatsAppCustomer?> GetByWaIdAsync(string waId, CancellationToken ct);
    Task<IReadOnlyList<WhatsAppCustomer>> ListByWaIdsAsync(IReadOnlyCollection<string> waIds, CancellationToken ct);
    Task<WhatsAppCustomer?> GetAsync(Guid id, uint? expectedVersion, CancellationToken ct);
    void Add(WhatsAppCustomer customer);
}
