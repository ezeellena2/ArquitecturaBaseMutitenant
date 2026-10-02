using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface ICompanyService
{
    Task<Result<PagedResult<CompanyResponse>>> ListAsync(ListCompaniesRequest request,
        CancellationToken cancellationToken);
    Task<Result<CompanyFilterCountsResponse>> GetFilterCountsAsync(ListCompaniesRequest request,
        CancellationToken cancellationToken);
    Task<Result<CompanyResponse>> GetAsync(Guid companyId, CancellationToken cancellationToken);
    Task<Result<Guid>> CreateAsync(CreateCompanyRequest request, CancellationToken cancellationToken);
    Task<Result> UpdateAsync(UpdateCompanyRequest request, CancellationToken cancellationToken);
    Task<Result> ChangeStatusAsync(ChangeCompanyStatusRequest request, CancellationToken cancellationToken);
}
