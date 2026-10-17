using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Models.Home;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Lee integrantes y sus relaciones dentro de la organización activa.</summary>
public interface IOrganizationUserReader
{
    Task<PagedResult<OrganizationUserRow>> ListAsync(ListOrganizationUsersRequest request, DateTime nowUtc,
        CancellationToken cancellationToken);
    Task<PagedResult<OrganizationUserRow>> ListPendingAsync(ListOrganizationUsersRequest request, DateTime nowUtc,
        CancellationToken cancellationToken) => ListAsync(request with
        {
            Page = PagedRequest.DefaultPage,
            PageSize = PendingGroup.PreviewLimit,
            PendingPreview = true,
        }, nowUtc, cancellationToken);
    Task<OrganizationUserFilterCountsResponse> GetFilterCountsAsync(ListOrganizationUsersRequest request,
        DateTime nowUtc, CancellationToken cancellationToken);
    Task<OrganizationUserRow?> FindByIdAsync(Guid memberId, DateTime nowUtc,
        CancellationToken cancellationToken);
}
