using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.PublicSite;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>Directorio público sin alcance privado ni caché; listado y logo exigen la misma publicación activa.</summary>
internal sealed class DirectoryReader(ApplicationDbContext context) : IDirectoryReader
{
    private static readonly SortMap<PublicPage> Sort = new()
    {
        ["name"] = page => page.Name,
    };

    private static readonly SortDescriptor DefaultSort = new("name", Descending: false);

    public async Task<PagedResult<DirectoryEntryRow>> ListAsync(ListDirectoryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await ListedPages(context.PublicPages.AsNoTracking())
            .ApplySearch(request.Search, page => page.Name)
            .ApplySort(SortDescriptor.Parse(request.Sort), Sort, DefaultSort, page => page.Id)
            .Select(page => new DirectoryPageRow(
                context.Tenants.Where(tenant => tenant.Id == page.BusinessTenantId).Select(tenant => tenant.Slug!).Single(),
                page.Name, page.Description, page.LogoBytes != null && page.LogoContentType != null, page.Version))
            .ToPagedResultAsync(request, cancellationToken);

        return new PagedResult<DirectoryEntryRow>([.. result.Items.Select(row => new DirectoryEntryRow(
            row.Slug, row.Name, row.Description,
            row.HasLogo ? "/api/directory/" + row.Slug + "/logo?v=" + VersionWireFormatter.Format(row.Version) : null))],
            result.Page, result.PageSize, result.TotalCount);
    }

    public Task<PublicSiteLogoResponse?> FindLogoAsync(string slug, CancellationToken cancellationToken) =>
        ListedPages(context.PublicPages.AsNoTracking())
            .Where(page => context.Tenants.Any(tenant => tenant.Id == page.BusinessTenantId && tenant.Slug == slug)
                && page.LogoBytes != null && page.LogoContentType != null)
            .Select(page => new PublicSiteLogoResponse(page.LogoBytes!, page.LogoContentType!, page.Version))
            .SingleOrDefaultAsync(cancellationToken);

    private IQueryable<PublicPage> ListedPages(IQueryable<PublicPage> pages) =>
        from page in pages
        join tenant in context.Tenants.AsNoTracking() on page.BusinessTenantId equals tenant.Id
        where page.IsPublished && page.PublishBlockedAtUtc == null
            && tenant.Kind == TenantKind.Business && tenant.Status == TenantStatus.Active
            && tenant.Slug != null
            && !context.Set<ReservedTenantSlug>().Any(reserved => reserved.Slug == tenant.Slug)
        select page;

    private sealed record DirectoryPageRow(string Slug, string Name, string? Description, bool HasLogo, uint Version);
}
