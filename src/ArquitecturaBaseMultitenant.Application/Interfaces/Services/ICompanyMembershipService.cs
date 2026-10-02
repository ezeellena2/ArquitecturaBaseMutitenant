using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface ICompanyMembershipService
{
    Task<Result<PagedResult<CompanyMemberResponse>>> ListAsync(Guid companyId,
        ListCompanyMembersRequest request, CancellationToken cancellationToken);
    Task<Result<CompanyMemberFilterCountsResponse>> GetFilterCountsAsync(Guid companyId,
        ListCompanyMembersRequest request, CancellationToken cancellationToken);
    Task<Result> AddAsync(AddCompanyMemberRequest request, CancellationToken cancellationToken);
    Task<Result> UpdateRolesAsync(UpdateCompanyMemberRolesRequest request, CancellationToken cancellationToken);
    Task<Result> ChangeStatusAsync(ChangeCompanyMemberStatusRequest request, CancellationToken cancellationToken);
}
