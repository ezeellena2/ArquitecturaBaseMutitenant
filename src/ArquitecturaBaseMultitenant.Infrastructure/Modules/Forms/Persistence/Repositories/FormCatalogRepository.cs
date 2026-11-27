using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Forms.Persistence.Repositories;

/// <summary>Prepara la escritura de los catálogos y sus opciones; el guardado ocurre en la UoW del caso de uso.</summary>
internal sealed class FormCatalogRepository(ApplicationDbContext context, ITenantContext tenantContext) : IFormCatalogRepository
{
    public Task LockAsync(IReadOnlyCollection<Guid> catalogIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(catalogIds);
        var tenantId = RequireTenant();
        return context.AcquireAdvisoryLocksAsync(catalogIds.Select(catalogId => AdvisoryLockKeys.For(tenantId, "forms-catalog", catalogId)), ct);
    }

    public async Task<FormCatalog?> GetByIdAsync(Guid catalogId, uint expectedVersion, CancellationToken ct)
    {
        var tenantId = RequireTenant();
        var catalog = await context.Set<FormCatalog>().SingleOrDefaultAsync(value => value.TenantId == tenantId && value.Id == catalogId, ct);
        if (catalog is not null) context.Entry(catalog).Property(value => value.Version).OriginalValue = expectedVersion;
        return catalog;
    }

    public async Task<IReadOnlyList<FormCatalogItem>> ListItemsAsync(Guid catalogId, IReadOnlyCollection<Guid> itemIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(itemIds);
        var tenantId = RequireTenant();
        if (itemIds.Count == 0) return [];
        return await Items(tenantId, catalogId).Where(item => itemIds.Contains(item.Id)).OrderBy(item => item.Position).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FormCatalogItem>> ListItemsByCodesAsync(Guid catalogId, IReadOnlyCollection<string> normalizedCodes,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(normalizedCodes);
        var tenantId = RequireTenant();
        if (normalizedCodes.Count == 0) return [];
        return await Items(tenantId, catalogId).Where(item => normalizedCodes.Contains(item.CodeNormalized)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FormCatalogItem>> ListAllItemsAsync(Guid catalogId, CancellationToken ct) =>
        await Items(RequireTenant(), catalogId).OrderBy(item => item.Position).ToListAsync(ct);

    public Task<int> CountItemsAsync(Guid catalogId, CancellationToken ct) => Items(RequireTenant(), catalogId).CountAsync(ct);

    public Task<int?> FindLastPositionAsync(Guid catalogId, CancellationToken ct) =>
        Items(RequireTenant(), catalogId).MaxAsync(item => (int?)item.Position, ct);

    public void Add(FormCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _ = RequireTenant();
        context.Set<FormCatalog>().Add(catalog);
    }

    public void Add(FormCatalogItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _ = RequireTenant();
        context.Set<FormCatalogItem>().Add(item);
    }

    public void MarkUpdated(FormCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _ = RequireTenant();
        context.Entry(catalog).State = EntityState.Modified;
    }

    public void Remove(FormCatalogItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _ = RequireTenant();
        context.Set<FormCatalogItem>().Remove(item);
    }

    private IQueryable<FormCatalogItem> Items(Guid tenantId, Guid catalogId) =>
        context.Set<FormCatalogItem>().Where(item => item.TenantId == tenantId && item.CatalogId == catalogId);

    private Guid RequireTenant()
    {
        context.RequireTransaction();
        return tenantContext.RequiredTenantId;
    }
}
