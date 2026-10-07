using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

internal sealed class PublicSiteReader(ApplicationDbContext context) : IPublicSiteReader
{
    public Task<PublicSiteRow?> FindBySlugAsync(string normalizedSlug, CancellationToken cancellationToken) =>
        (from page in context.PublicPages.AsNoTracking()
         join tenant in context.Tenants.AsNoTracking() on page.BusinessTenantId equals tenant.Id
         where tenant.Slug == normalizedSlug
             && tenant.Kind == TenantKind.Business
             && tenant.Status == TenantStatus.Active
             && page.IsPublished
             && page.PublishBlockedAtUtc == null
             && !context.Set<ReservedTenantSlug>().Any(reserved => reserved.Slug == normalizedSlug)
         select new PublicSiteRow(tenant.Id, tenant.Slug!))
        .SingleOrDefaultAsync(cancellationToken);
}
