using ArquitecturaBaseMultitenant.Application.Models.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface ITenantDomainReader
{
    Task<IReadOnlyList<OrganizationDomainRow>> ListCurrentAsync(CancellationToken cancellationToken);
    Task<OrganizationDomainRow?> FindCurrentByIdAsync(Guid domainId, CancellationToken cancellationToken);
}
