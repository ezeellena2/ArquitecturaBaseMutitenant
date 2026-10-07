using ArquitecturaBaseMultitenant.Application.Models.PublicSite;
using ArquitecturaBaseMultitenant.Application.Models.Platform;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface IPublicPageReader
{
    Task<PublicPageRow?> FindCurrentAsync(CancellationToken cancellationToken);
    Task<PlatformPublicSiteResponse?> FindModerationAsync(string? slug, CancellationToken cancellationToken);
    Task<PublicSitePageRow?> FindPublishedAsync(Guid businessTenantId, string slug, CancellationToken cancellationToken);
    Task<PublicSiteLogoResponse?> FindPublishedLogoAsync(Guid businessTenantId, string slug, CancellationToken cancellationToken);
}
