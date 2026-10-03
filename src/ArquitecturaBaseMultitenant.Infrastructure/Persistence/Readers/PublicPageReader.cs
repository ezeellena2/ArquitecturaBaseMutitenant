using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.PublicSite;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

internal sealed class PublicPageReader(ApplicationDbContext context, ITenantContext tenantContext)
    : IPublicPageReader
{
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
