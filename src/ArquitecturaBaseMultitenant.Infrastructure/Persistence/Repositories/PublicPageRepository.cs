using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

internal sealed class PublicPageRepository(ApplicationDbContext context, ITenantContext tenantContext)
    : IPublicPageRepository
{
    public async Task<PublicPage?> GetCurrentAsync(uint expectedVersion, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var tenantId = tenantContext.RequiredTenantId;
        var page = await context.PublicPages.SingleOrDefaultAsync(
            row => row.BusinessTenantId == tenantId, cancellationToken);
        if (page is not null)
            context.Entry(page).Property(candidate => candidate.Version).OriginalValue = expectedVersion;
        return page;
    }

    public void Add(PublicPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        context.PublicPages.Add(page);
    }

    public void MarkUpdated(PublicPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        context.Entry(page).State = EntityState.Modified;
    }
}
