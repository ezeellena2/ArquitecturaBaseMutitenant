using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Prepara cambios de roles dentro de la transacción del servicio llamador.</summary>
public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(Guid roleId, uint expectedVersion, CancellationToken cancellationToken);

    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken);

    void Add(Role role);

    void MarkUpdated(Role role);

    void Remove(Role role);
}
