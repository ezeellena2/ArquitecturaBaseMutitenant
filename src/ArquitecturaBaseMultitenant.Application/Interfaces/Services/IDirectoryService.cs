using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IDirectoryService
{
    Task<Result<PagedResult<DirectoryEntryRow>>> ListAsync(ListDirectoryRequest request, CancellationToken cancellationToken);
    Task<Result<PublicSiteLogoResponse>> GetLogoAsync(string slug, CancellationToken cancellationToken);
}
