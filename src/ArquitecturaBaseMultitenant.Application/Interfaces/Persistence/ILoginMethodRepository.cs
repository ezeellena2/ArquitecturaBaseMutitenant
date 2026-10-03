using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Bloquea y modifica métodos de ingreso de una identidad global dentro de la transacción que aplica sus reglas.</summary>
public interface ILoginMethodRepository
{
    Task LockUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<LoginMethod?> GetByIdForUserAsync(Guid userId, Guid methodId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LoginMethod>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task ClearPrimaryAsync(Guid userId, CancellationToken cancellationToken);
    Task<LoginMethod?> GetByIdAsync(Guid methodId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LoginMethod>> ListVerifiedByEmailDomainAsync(OrganizationEmailDomain domain,
        CancellationToken cancellationToken);

    void Add(LoginMethod method);
    void Remove(LoginMethod method);
}
