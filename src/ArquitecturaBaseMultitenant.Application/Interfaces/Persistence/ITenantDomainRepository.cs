using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface ITenantDomainRepository
{
    Task LockDomainAsync(OrganizationEmailDomain domain, CancellationToken cancellationToken);
    Task<TenantDomain?> GetByIdAsync(Guid domainId, CancellationToken cancellationToken);
    Task<TenantDomain?> GetByIdAsync(Guid domainId, uint expectedVersion, CancellationToken cancellationToken);
    Task<TenantDomain?> FindActiveByDomainAsync(OrganizationEmailDomain domain, CancellationToken cancellationToken);
    Task<Guid?> FindVerifiedOrganizationIdAsync(OrganizationEmailDomain domain, CancellationToken cancellationToken);
    void Add(TenantDomain domain);
    void MarkUpdated(TenantDomain domain);
}
