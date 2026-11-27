using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;

/// <summary>Las lecturas de los catálogos de la organización activa, sin transacción ni seguimiento.</summary>
public interface IFormCatalogReader
{
    /// <summary>
    /// El listado, con orden estable (264). <c>optionsCount</c> ordena por las opciones de uno a mano, y los demás quedan al
    /// final; <c>status</c>, por activo, sin opciones activas y dado de baja.
    /// </summary>
    Task<PagedResult<FormCatalogSummary>> ListAsync(ListFormCatalogsRequest request, CancellationToken ct);

    /// <summary>Solo lo que dice el resumen del listado: todos y los activos (262).</summary>
    Task<FormCatalogFilterCountsResponse> CountFiltersAsync(CancellationToken ct);

    Task<FormCatalogSummary?> FindAsync(Guid catalogId, CancellationToken ct);

    /// <summary>Las opciones de un catálogo a mano, por posición, con el aviso del texto repetido.</summary>
    Task<PagedResult<FormCatalogItemRow>> ListItemsAsync(Guid catalogId, ListFormCatalogItemsRequest request, CancellationToken ct);

    /// <summary>
    /// Un lote de opciones, activas y dadas de baja, por posición y después de <paramref name="afterPosition"/>: exportar y revisar
    /// un reemplazo leen en lotes, sin traer las 10.000 de una vez.
    /// </summary>
    Task<IReadOnlyList<FormCatalogItemSnapshot>> ListItemBatchAsync(Guid catalogId, int? afterPosition, int take, CancellationToken ct);

    /// <summary>Las opciones del catálogo que tienen guardado alguno de esos códigos normalizados: lo que revisa lo pegado.</summary>
    /// <summary>Las opciones del catálogo con esos ids, sin las de otro: lo que vuelve a leer una propuesta guardada (ADR 0068 §9).</summary>
    Task<IReadOnlyList<FormCatalogItemSnapshot>> ListItemsByIdsAsync(Guid catalogId, IReadOnlyCollection<Guid> itemIds, CancellationToken ct);

    Task<IReadOnlyList<FormCatalogItemSnapshot>> ListItemsByCodesAsync(Guid catalogId, IReadOnlyCollection<string> normalizedCodes,
        CancellationToken ct);
}
