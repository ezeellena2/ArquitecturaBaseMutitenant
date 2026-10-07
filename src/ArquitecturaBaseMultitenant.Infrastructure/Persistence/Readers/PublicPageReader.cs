using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.PublicSite;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

internal sealed class PublicPageReader(ApplicationDbContext context, ITenantContext tenantContext)
    : IPublicPageReader
{
    public Task<PublicSitePageRow?> FindPublishedAsync(Guid businessTenantId, string slug,
        CancellationToken cancellationToken) =>
        PublishedPage(businessTenantId, slug)
            .Select(page => new PublicSitePageRow(page.Name, page.Description, page.Address,
                page.ContactPhone, page.ContactEmail, page.Website,
                page.LogoBytes != null && page.LogoContentType != null, page.Version))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<PublicSiteLogoResponse?> FindPublishedLogoAsync(Guid businessTenantId, string slug,
        CancellationToken cancellationToken) =>
        PublishedPage(businessTenantId, slug)
            .Where(page => page.LogoBytes != null && page.LogoContentType != null)
            .Select(page => new PublicSiteLogoResponse(page.LogoBytes!, page.LogoContentType!, page.Version))
            .SingleOrDefaultAsync(cancellationToken);

    private IQueryable<PublicPage> PublishedPage(Guid businessTenantId, string slug) =>
        context.PublicPages.AsNoTracking()
            .Where(page => page.BusinessTenantId == businessTenantId && page.IsPublished
                && page.PublishBlockedAtUtc == null
                && context.Tenants.Any(tenant => tenant.Id == page.BusinessTenantId
                    && tenant.Slug == slug && tenant.Kind == TenantKind.Business && tenant.Status == TenantStatus.Active)
                && !context.Set<ReservedTenantSlug>().Any(reserved => reserved.Slug == slug));

    public Task<PlatformPublicSiteResponse?> FindModerationAsync(string? slug, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;
        return context.PublicPages.AsNoTracking().Where(page => page.BusinessTenantId == tenantId)
            .Select(page => new PlatformPublicSiteResponse(slug, page.Name, page.IsPublished,
                page.PublishBlockedAtUtc, page.PublishBlockedReason, page.Version, page.PublishedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<PublicPageRow?> FindCurrentAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;
        return context.PublicPages.AsNoTracking()
            .Where(page => page.BusinessTenantId == tenantId)
            .Select(page => new PublicPageRow(page.Name, page.Description, page.LogoBytes,
                page.LogoContentType, page.ContactEmail, page.ContactPhone, page.Address, page.Website,
                page.IsPublished, page.PublishBlockedAtUtc, page.PublishBlockedReason, page.Version))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
