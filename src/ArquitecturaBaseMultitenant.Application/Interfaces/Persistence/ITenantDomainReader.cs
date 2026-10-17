using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Home;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface ITenantDomainReader
{
    Task<PagedResult<OrganizationDomainRow>> ListPagedAsync(ListOrganizationDomainsRequest request, CancellationToken cancellationToken);
    Task<PagedResult<OrganizationDomainRow>> ListPendingAsync(CancellationToken cancellationToken) =>
        ListPagedAsync(new ListOrganizationDomainsRequest { PendingPreview = true, PageSize = PendingGroup.PreviewLimit }, cancellationToken);
    async Task<string?> FindHomeDomainAsync(CancellationToken cancellationToken) =>
        (await ListCurrentAsync(cancellationToken)).OrderBy(domain => domain.Status != TenantDomainStatus.Verified)
            .ThenBy(domain => domain.Domain).Select(domain => domain.Domain).FirstOrDefault();
    Task<IReadOnlyList<OrganizationDomainRow>> ListCurrentAsync(CancellationToken cancellationToken);
    Task<OrganizationDomainRow?> FindCurrentByIdAsync(Guid domainId, CancellationToken cancellationToken);
}
