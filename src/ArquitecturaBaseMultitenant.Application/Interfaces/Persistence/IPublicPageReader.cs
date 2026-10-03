using ArquitecturaBaseMultitenant.Application.Models.PublicSite;
using ArquitecturaBaseMultitenant.Application.Models.Platform;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface IPublicPageReader
{
    Task<PublicPageRow?> FindCurrentAsync(CancellationToken cancellationToken);
    Task<PlatformPublicSiteResponse?> FindModerationAsync(string? slug, CancellationToken cancellationToken);
}
