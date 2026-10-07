using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.PublicSite;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.PublicSite;

internal sealed class PublicSiteService(IPublicPageReader pages, IPublicSiteContext publicSite,
    TimeProvider timeProvider, ILogger<PublicSiteService> logger) : IPublicSiteService
{
    public Task<Result<PublicSiteResponse>> GetAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PublicSiteResponse>(logger, timeProvider, "GetPublicSite", async () =>
        {
            if (publicSite.BusinessTenantId is not { } tenantId || publicSite.Slug is not { } slug)
                return PublicPageErrors.NotFound;

            var page = await pages.FindPublishedAsync(tenantId, slug, cancellationToken);
            if (page is null) return PublicPageErrors.NotFound;

            var logoUrl = page.HasLogo
                ? "/api/public-site/logo?v=" + page.Version.ToString(CultureInfo.InvariantCulture)
                : null;
            return new PublicSiteResponse(slug, page.Name, page.Description, logoUrl,
                page.Address, page.Phone, page.Email, page.Website);
        });

    public Task<Result<PublicSiteLogoResponse>> GetLogoAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PublicSiteLogoResponse>(logger, timeProvider, "GetPublicSiteLogo", async () =>
        {
            if (publicSite.BusinessTenantId is not { } tenantId || publicSite.Slug is not { } slug)
                return PublicPageErrors.NotFound;

            var logo = await pages.FindPublishedLogoAsync(tenantId, slug, cancellationToken);
            return logo is null ? PublicPageErrors.NotFound : logo;
        });
}
