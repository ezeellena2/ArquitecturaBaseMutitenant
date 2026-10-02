using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Companies;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Lee empresas del tenant activo sin abrir una transacción de escritura.</summary>
public interface ICompanyReader
{
    Task<PagedResult<CompanyRow>> ListAsync(ListCompaniesRequest request, CancellationToken cancellationToken);
    Task<CompanyRow?> FindByIdAsync(Guid companyId, CancellationToken cancellationToken);
    Task<CompanyFilterCountsResponse> GetFilterCountsAsync(ListCompaniesRequest request,
        CancellationToken cancellationToken);
    Task<bool> ExistsTaxIdAsync(string country, string type, string number, Guid? excludedCompanyId,
        CancellationToken cancellationToken);
}
