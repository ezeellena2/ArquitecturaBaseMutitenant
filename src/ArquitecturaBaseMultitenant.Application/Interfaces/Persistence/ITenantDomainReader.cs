using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface ITenantDomainReader
{
    Task<PagedResult<OrganizationDomainRow>> ListPagedAsync(ListOrganizationDomainsRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrganizationDomainRow>> ListCurrentAsync(CancellationToken cancellationToken);
    Task<OrganizationDomainRow?> FindCurrentByIdAsync(Guid domainId, CancellationToken cancellationToken);
}
