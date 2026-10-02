using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Prepara altas y cambios de empresas dentro de la transacción de su caso de uso.</summary>
public interface ICompanyRepository
{
    Task<Company?> GetByIdAsync(Guid companyId, uint expectedVersion, CancellationToken cancellationToken);

    void Add(Company company);

    void MarkUpdated(Company company);

    void Remove(Company company);
}
