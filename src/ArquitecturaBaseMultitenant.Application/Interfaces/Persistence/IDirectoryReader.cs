using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.PublicSite;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface IDirectoryReader
{
    Task<PagedResult<DirectoryEntryRow>> ListAsync(ListDirectoryRequest request, CancellationToken cancellationToken);
    Task<PublicSiteLogoResponse?> FindLogoAsync(string slug, CancellationToken cancellationToken);
}
