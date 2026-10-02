using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Lee integrantes y sus relaciones dentro de la organización activa.</summary>
public interface IOrganizationUserReader
{
    Task<PagedResult<OrganizationUserRow>> ListAsync(ListOrganizationUsersRequest request, DateTime nowUtc,
        CancellationToken cancellationToken);
    Task<OrganizationUserFilterCountsResponse> GetFilterCountsAsync(ListOrganizationUsersRequest request,
        DateTime nowUtc, CancellationToken cancellationToken);
    Task<OrganizationUserRow?> FindByIdAsync(Guid memberId, DateTime nowUtc,
        CancellationToken cancellationToken);
}
