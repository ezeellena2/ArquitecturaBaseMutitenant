using System.Text.Json;
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

    public async Task LockWithSourcesAsync(Guid catalogId, IReadOnlyCollection<Guid> requestedSourceIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requestedSourceIds);
        var tenantId = RequireTenant();
        var fieldsJson = await context.Set<FormCatalog>().AsNoTracking()
            .Where(catalog => catalog.TenantId == tenantId && catalog.Id == catalogId)
            .Select(catalog => catalog.FieldsJson)
            .SingleOrDefaultAsync(ct);
        IEnumerable<Guid> stored = fieldsJson is null ? [] : FormCatalogField.ListFromJson(fieldsJson)
            .Where(field => field.IsActive && field.SourceCatalogId is not null)
            .Select(field => field.SourceCatalogId!.Value);
        await LockAsync([catalogId, .. stored, .. requestedSourceIds], ct);
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

    // El valor de la clave como texto (->>), sin espacios en los extremos y en mayúsculas, como lo compara el servicio. Sin índice:
    // filtra las opciones del catálogo, que usan el de CatalogId, y devuelve solo las que chocan.
    public async Task<IReadOnlyList<FormCatalogItem>> ListActiveItemsByFieldValuesAsync(Guid catalogId, string key,
        IReadOnlyCollection<string> upperValues, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(upperValues);
        var tenantId = RequireTenant();
        if (upperValues.Count == 0) return [];
        string[] values = [.. upperValues];
        return await context.Set<FormCatalogItem>().FromSql($"""
                SELECT * FROM tenant."FormCatalogItems"
                WHERE "TenantId" = {tenantId} AND "CatalogId" = {catalogId} AND "IsActive"
                    AND upper(btrim("FieldValuesJson" ->> {key})) = ANY({values})
                """)
            .Where(item => item.TenantId == tenantId && item.CatalogId == catalogId)
            .ToListAsync(ct);
    }

    public Task<bool> IsFieldSourceAsync(Guid catalogId, CancellationToken ct)
    {
        var tenantId = RequireTenant();
        var probe = FieldSourceProbe(catalogId);
        return context.Database.SqlQuery<bool>($"""
                SELECT EXISTS (
                    SELECT 1 FROM tenant."FormCatalogs"
                    WHERE "TenantId" = {tenantId} AND "Id" <> {catalogId} AND "FieldsJson" @> {probe}::jsonb) AS "Value"
                """)
            .SingleAsync(ct);
    }

    // Primero los campos activos de otros catálogos que toman sus valores de este (FieldsJson @>, sin índice); después, una sola
    // consulta con un par (catálogo, {"clave": "opción"}) por cada clave y cada opción, que el GIN de FieldValuesJson resuelve con @>.
    public async Task<IReadOnlyList<Guid>> ListItemsUsedByOptionsAsync(Guid catalogId, IReadOnlyCollection<Guid> itemIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(itemIds);
        var tenantId = RequireTenant();
        if (itemIds.Count == 0) return [];
        var probe = FieldSourceProbe(catalogId);
        var users = await context.Set<FormCatalog>().FromSql($"""
                SELECT * FROM tenant."FormCatalogs"
                WHERE "TenantId" = {tenantId} AND "Id" <> {catalogId} AND "FieldsJson" @> {probe}::jsonb
                """)
            .AsNoTracking()
            .Where(catalog => catalog.TenantId == tenantId)
            .Select(catalog => new { catalog.Id, catalog.FieldsJson })
            .ToListAsync(ct);

        var catalogIds = new List<Guid>();
        var optionIds = new List<Guid>();
        var values = new List<string>();
        foreach (var user in users)
        {
            var keys = FormCatalogField.ListFromJson(user.FieldsJson)
                .Where(field => field.IsActive && field.SourceCatalogId == catalogId)
                .Select(field => field.Key);
            foreach (var key in keys)
                foreach (var itemId in itemIds)
                {
                    catalogIds.Add(user.Id);
                    optionIds.Add(itemId);
                    values.Add(JsonSerializer.Serialize(new Dictionary<string, string> { [key] = itemId.ToString("D") }));
                }
        }

        if (values.Count == 0) return [];
        Guid[] byCatalog = [.. catalogIds];
        Guid[] byOption = [.. optionIds];
        string[] byValue = [.. values];
        return await context.Database.SqlQuery<Guid>($"""
                SELECT DISTINCT probe.item_id AS "Value"
                FROM unnest({byCatalog}, {byOption}, {byValue}::jsonb[]) AS probe(catalog_id, item_id, value)
                WHERE EXISTS (
                    SELECT 1 FROM tenant."FormCatalogItems" AS item
                    WHERE item."TenantId" = {tenantId} AND item."CatalogId" = probe.catalog_id AND item."FieldValuesJson" @> probe.value)
                """)
            .ToListAsync(ct);
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

    /// <summary>Lo que contiene la lista de campos de un catálogo con un campo activo que toma sus valores de <paramref name="catalogId"/>.</summary>
    private static string FieldSourceProbe(Guid catalogId) =>
        JsonSerializer.Serialize(new[] { new { sourceCatalogId = catalogId.ToString("D"), isActive = true } });

    private IQueryable<FormCatalogItem> Items(Guid tenantId, Guid catalogId) =>
        context.Set<FormCatalogItem>().Where(item => item.TenantId == tenantId && item.CatalogId == catalogId);

    private Guid RequireTenant()
    {
        context.RequireTransaction();
        return tenantContext.RequiredTenantId;
    }
}
