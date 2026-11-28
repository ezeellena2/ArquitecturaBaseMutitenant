using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Forms.Persistence.Readers;

/// <summary>Proyecta los catálogos de la organización activa y sus opciones, con orden estable y búsqueda sin acentos.</summary>
internal sealed class FormCatalogReader(ApplicationDbContext context, ITenantContext tenantContext) : IFormCatalogReader
{
    // El origen y el tipo ordenan por su orden, con el CASE del enum (264). Las opciones son las de uno a mano: los demás no
    // tienen cuenta y quedan al final. El estado va de activo a sin opciones activas y a dado de baja; el resto de «No se puede
    // usar» se calcula al leer, con la fuente.
    private static readonly SortMap<CatalogProjection> Sort = new()
    {
        ["name"] = catalog => catalog.Name,
        ["source"] = catalog => catalog.Source,
        ["valueType"] = catalog => catalog.ValueType,
        ["optionsCount"] = catalog => catalog.OptionsCount,
        ["status"] = catalog => catalog.Status,
    };

    private static readonly SortDescriptor DefaultSort = new("name", Descending: false);

    public async Task<PagedResult<FormCatalogStoredRow>> ListAsync(ListFormCatalogsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.RequiredTenantId;
        var catalogs = context.Set<FormCatalog>().AsNoTracking().Where(catalog => catalog.TenantId == tenantId)
            .ApplySearch(request.Search, catalog => catalog.Name);
        catalogs = request.Source switch
        {
            FormCatalogSourceFilter.Manual => catalogs.Where(catalog => catalog.Source == FormCatalogSource.Manual),
            FormCatalogSourceFilter.Members => catalogs.Where(catalog => catalog.Source == FormCatalogSource.Members),
            FormCatalogSourceFilter.Contacts => catalogs.Where(catalog => catalog.Source == FormCatalogSource.Contacts),
            _ => catalogs,
        };
        catalogs = request.Status switch
        {
            FormCatalogStatusFilter.Active => catalogs.Where(catalog => catalog.IsActive),
            FormCatalogStatusFilter.Inactive => catalogs.Where(catalog => !catalog.IsActive),
            _ => catalogs,
        };
        var page = await Project(tenantId, catalogs)
            .ApplySort(SortDescriptor.Parse(request.Sort), Sort, DefaultSort, catalog => catalog.Id)
            .ToPagedResultAsync(request, ct);
        return new PagedResult<FormCatalogStoredRow>([.. page.Items.Select(ToSummary)], page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<FormCatalogFilterCountsResponse> CountFiltersAsync(CancellationToken ct)
    {
        var tenantId = tenantContext.RequiredTenantId;
        var counts = await context.Set<FormCatalog>().AsNoTracking().Where(catalog => catalog.TenantId == tenantId)
            .GroupBy(catalog => catalog.IsActive).Select(group => new { IsActive = group.Key, Count = group.Count() })
            .ToListAsync(ct);
        return new FormCatalogFilterCountsResponse(counts.Sum(row => row.Count), counts.Where(row => row.IsActive).Sum(row => row.Count));
    }

    public async Task<FormCatalogStoredRow?> FindAsync(Guid catalogId, CancellationToken ct)
    {
        var tenantId = tenantContext.RequiredTenantId;
        var catalogs = context.Set<FormCatalog>().AsNoTracking().Where(catalog => catalog.TenantId == tenantId && catalog.Id == catalogId);
        var row = await Project(tenantId, catalogs).SingleOrDefaultAsync(ct);
        return row is null ? null : ToSummary(row);
    }

    public async Task<PagedResult<FormCatalogItemRow>> ListItemsAsync(Guid catalogId, ListFormCatalogItemsRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.RequiredTenantId;
        var all = context.Set<FormCatalogItem>().AsNoTracking().Where(item => item.TenantId == tenantId && item.CatalogId == catalogId);
        var items = all.ApplySearch(request.Search, item => item.Text, item => item.Code);
        items = request.Status switch
        {
            FormCatalogStatusFilter.Active => items.Where(item => item.IsActive),
            FormCatalogStatusFilter.Inactive => items.Where(item => !item.IsActive),
            _ => items,
        };
        // Un texto repetido avisa sin frenar: otra opción del catálogo con el mismo texto, sin distinguir mayúsculas. EF lo
        // traduce a lower() de PostgreSQL, como ApplySearch: la cultura del proceso no interviene. Se busca después, solo para los
        // textos de la página y en una sola pasada: calcularlo en cada fila recorría el catálogo entero por cada opción salteada.
#pragma warning disable CA1304, CA1311, CA1862
        var page = await items.OrderBy(item => item.Position).ThenBy(item => item.Id)
            .Select(item => new ItemProjection
            {
                Id = item.Id,
                Code = item.Code,
                Text = item.Text,
                Detail = item.Detail,
                DateValue = item.DateValue,
                NumberValue = item.NumberValue,
                Position = item.Position,
                IsActive = item.IsActive,
                LowerText = item.Text.ToLower(),
            })
            .ToPagedResultAsync(request, ct);
        var lowerTexts = page.Items.Select(row => row.LowerText).Distinct(StringComparer.Ordinal).ToArray();
        HashSet<string> repeated = lowerTexts.Length == 0 ? [] : (await all.Where(item => lowerTexts.Contains(item.Text.ToLower()))
            .GroupBy(item => item.Text.ToLower()).Where(group => group.Count() > 1).Select(group => group.Key).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
#pragma warning restore CA1304, CA1311, CA1862
        return new PagedResult<FormCatalogItemRow>([.. page.Items.Select(row => new FormCatalogItemRow(row.Id, row.Code, row.Text,
            row.Detail, FormCatalogValueText.Of(row.DateValue, row.NumberValue), row.Position, row.IsActive, repeated.Contains(row.LowerText),
            FormCatalogItemRow.NoFields, null))],
            page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<IReadOnlyList<FormCatalogStoredItemRow>> ListItemBatchAsync(Guid catalogId, int? afterPosition, int take,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);
        var items = Items(catalogId);
        if (afterPosition is { } after) items = items.Where(item => item.Position > after);
        return await Stored(items.OrderBy(item => item.Position).Take(take)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FormCatalogStoredItemRow>> ListItemsByIdsAsync(Guid catalogId, IReadOnlyCollection<Guid> itemIds,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(itemIds);
        if (itemIds.Count == 0) return [];
        return await Stored(Items(catalogId).Where(item => itemIds.Contains(item.Id)).OrderBy(item => item.Position)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FormCatalogStoredItemRow>> ListItemsByCodesAsync(Guid catalogId,
        IReadOnlyCollection<string> normalizedCodes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(normalizedCodes);
        if (normalizedCodes.Count == 0) return [];
        return await Stored(Items(catalogId).Where(item => normalizedCodes.Contains(item.CodeNormalized)).OrderBy(item => item.Position))
            .ToListAsync(ct);
    }

    // Una búsqueda por fila de unnest y, para cada una, sus parecidos como los busca ListItemsAsync (ApplySearch: sin acentos ni
    // mayúsculas, con los comodines escapados), por posición y con su tope, en una sola consulta.
    public async Task<IReadOnlyList<FormCatalogItemCandidateRow>> ListItemCandidatesAsync(Guid catalogId,
        IReadOnlyCollection<string> searches, int perSearch, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(searches);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(perSearch);
        string[] distinct = [.. searches.Where(search => !string.IsNullOrWhiteSpace(search)).Distinct(StringComparer.Ordinal)];
        if (distinct.Length == 0) return [];
        var tenantId = tenantContext.RequiredTenantId;
        string[] patterns = [.. distinct.Select(search => $"%{QueryableExtensions.EscapeLike(search.Trim())}%")];
        var rows = await context.Database.SqlQuery<CandidateProjection>($"""
            SELECT wanted."Search", found."Id", found."Code", found."Text"
            FROM unnest({distinct}::text[], {patterns}::text[]) AS wanted("Search", "Pattern")
            CROSS JOIN LATERAL (
                SELECT item."Id", item."Code", item."Text", item."Position"
                FROM tenant."FormCatalogItems" AS item
                WHERE item."TenantId" = {tenantId} AND item."CatalogId" = {catalogId}
                    AND (public.f_unaccent(lower(item."Text")) LIKE public.f_unaccent(lower(wanted."Pattern")) ESCAPE '\'
                        OR public.f_unaccent(lower(item."Code")) LIKE public.f_unaccent(lower(wanted."Pattern")) ESCAPE '\')
                ORDER BY item."Position", item."Id"
                LIMIT {perSearch}) AS found
            ORDER BY wanted."Search", found."Position", found."Id"
            """).ToListAsync(ct);
        return [.. rows.Select(row => new FormCatalogItemCandidateRow(row.Search, row.Id, row.Code, row.Text))];
    }

    private IQueryable<FormCatalogItem> Items(Guid catalogId)
    {
        var tenantId = tenantContext.RequiredTenantId;
        return context.Set<FormCatalogItem>().AsNoTracking().Where(item => item.TenantId == tenantId && item.CatalogId == catalogId);
    }

    // La fila en la consulta, sin cargar la entidad: solo sus columnas, y el valor canónico armado con las dos del valor.
    private static IQueryable<FormCatalogStoredItemRow> Stored(IQueryable<FormCatalogItem> items) =>
        items.Select(item => new FormCatalogStoredItemRow(item.Id, item.Code, item.CodeNormalized, item.Text, item.Detail,
            FormCatalogValueText.Of(item.DateValue, item.NumberValue), item.Position, item.IsActive));

    private IQueryable<CatalogProjection> Project(Guid tenantId, IQueryable<FormCatalog> catalogs) =>
        catalogs.Select(catalog => new CatalogProjection
        {
            Id = catalog.Id,
            Name = catalog.Name,
            Description = catalog.Description,
            Source = catalog.Source,
            ValueType = catalog.ValueType,
            Filters = catalog.Filters,
            IsActive = catalog.IsActive,
            TotalItems = context.Set<FormCatalogItem>().Count(item => item.TenantId == tenantId && item.CatalogId == catalog.Id),
            ActiveItems = context.Set<FormCatalogItem>()
                .Count(item => item.TenantId == tenantId && item.CatalogId == catalog.Id && item.IsActive),
            OptionsCount = catalog.Source == FormCatalogSource.Manual
                ? context.Set<FormCatalogItem>().Count(item => item.TenantId == tenantId && item.CatalogId == catalog.Id)
                : null,
            Status = !catalog.IsActive ? 2
                : catalog.Source == FormCatalogSource.Manual && !context.Set<FormCatalogItem>()
                    .Any(item => item.TenantId == tenantId && item.CatalogId == catalog.Id && item.IsActive) ? 1 : 0,
            ModifiedAtUtc = catalog.ModifiedAtUtc ?? catalog.CreatedAtUtc,
            ModifiedBy = catalog.ModifiedBy ?? catalog.CreatedBy,
            Version = catalog.Version,
        });

    private static FormCatalogStoredRow ToSummary(CatalogProjection row) => new(row.Id, row.Name, row.Description, row.Source,
        row.ValueType, row.Filters is null ? null : Domain.Forms.FormCatalogFilters.FromJson(row.Filters), row.IsActive, row.TotalItems,
        row.ActiveItems, DateTime.SpecifyKind(row.ModifiedAtUtc, DateTimeKind.Utc), row.ModifiedBy, row.Version);

    /// <summary>Un catálogo con lo que se ordena y se cuenta en la base.</summary>
    private sealed class CatalogProjection
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public FormCatalogSource Source { get; init; }
        public FormCatalogValueType ValueType { get; init; }
        public string? Filters { get; init; }
        public bool IsActive { get; init; }
        public int TotalItems { get; init; }
        public int ActiveItems { get; init; }
        public int? OptionsCount { get; init; }
        public int Status { get; init; }
        public DateTime ModifiedAtUtc { get; init; }
        public Guid? ModifiedBy { get; init; }
        public uint Version { get; init; }
    }

    private sealed class ItemProjection
    {
        public Guid Id { get; init; }
        public string Code { get; init; } = null!;
        public string Text { get; init; } = null!;
        public string? Detail { get; init; }
        public DateOnly? DateValue { get; init; }
        public decimal? NumberValue { get; init; }
        public int Position { get; init; }
        public bool IsActive { get; init; }
        public string LowerText { get; init; } = null!;
    }

    /// <summary>Un parecido de una búsqueda, como lo devuelve el SQL.</summary>
    private sealed class CandidateProjection
    {
        public string Search { get; init; } = null!;
        public Guid Id { get; init; }
        public string Code { get; init; } = null!;
        public string Text { get; init; } = null!;
    }
}
