using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Obtiene y agrega organizaciones desde casos de uso de alta o gestión; el guardado pertenece a la UnitOfWork.</summary>
public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken);

    void Add(Tenant tenant);
}
