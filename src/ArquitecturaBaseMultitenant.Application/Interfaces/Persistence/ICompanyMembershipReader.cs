using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Companies;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Proyecta miembros de una empresa del tenant activo sin exponer entidades seguidas.</summary>
public interface ICompanyMembershipReader
{
    Task<PagedResult<CompanyMemberRow>> ListAsync(Guid companyId, ListCompanyMembersRequest request,
        CancellationToken cancellationToken);
    Task<CompanyMemberFilterCountsResponse> GetFilterCountsAsync(Guid companyId,
        ListCompanyMembersRequest request, CancellationToken cancellationToken);
}
