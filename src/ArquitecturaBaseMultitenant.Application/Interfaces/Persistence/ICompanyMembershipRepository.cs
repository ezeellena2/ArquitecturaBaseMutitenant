using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Prepara cambios de pertenencia a empresa dentro de la transacción del servicio llamador.</summary>
public interface ICompanyMembershipRepository
{
    Task<CompanyMembership?> GetByIdAsync(Guid companyId, Guid memberId, uint expectedVersion,
        CancellationToken cancellationToken);
    Task<CompanyMembership?> FindAsync(Guid companyId, Guid memberId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CompanyMembership>> ListForMemberAsync(Guid memberId, CancellationToken cancellationToken);
    void Add(CompanyMembership membership);
    void MarkUpdated(CompanyMembership membership);
    void Remove(CompanyMembership membership);
}
