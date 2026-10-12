using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IOrganizationDomainService
{
    Task<Result<PagedResult<OrganizationDomainResponse>>> ListPagedAsync(ListOrganizationDomainsRequest request, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<OrganizationDomainResponse>>> ListAsync(CancellationToken cancellationToken);
    Task<Result<OrganizationDomainResponse>> RegisterAsync(RegisterOrganizationDomainRequest request,
        CancellationToken cancellationToken);
    Task<Result> VerifyAsync(Guid domainId, CancellationToken cancellationToken);
    Task<Result> RemoveAsync(RemoveOrganizationDomainRequest request, CancellationToken cancellationToken);
}
