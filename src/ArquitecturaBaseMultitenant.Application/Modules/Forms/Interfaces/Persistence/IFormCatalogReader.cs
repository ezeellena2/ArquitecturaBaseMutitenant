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
    Task<PagedResult<FormCatalogStoredRow>> ListAsync(ListFormCatalogsRequest request, CancellationToken ct);

    /// <summary>Solo lo que dice el resumen del listado: todos y los activos (262).</summary>
    Task<FormCatalogFilterCountsResponse> CountFiltersAsync(CancellationToken ct);

    Task<FormCatalogStoredRow?> FindAsync(Guid catalogId, CancellationToken ct);

    /// <summary>
    /// Los campos y la presentación guardados de un catálogo, con cuántas opciones tienen un valor de cada clave (E13): un solo
    /// <c>GROUP BY</c>, que un texto vacío no cuenta. <c>null</c> si el catálogo no es de la organización.
    /// </summary>
    Task<FormCatalogFieldsRow?> FindFieldsAsync(Guid catalogId, CancellationToken ct);

    /// <summary>
    /// Los campos y la presentación guardados, sin contar valores: lo que arma el título y la descripción de una página de opciones
    /// (E13). <c>null</c> si el catálogo no es de la organización.
    /// </summary>
    Task<FormCatalogPresentationRow?> FindPresentationAsync(Guid catalogId, CancellationToken ct);

    /// <summary>Los catálogos de la organización con esos ids, con su nombre, su origen y su estado: las fuentes de los campos (111).</summary>
    Task<IReadOnlyList<FormCatalogFieldSourceRow>> ListFieldSourcesAsync(IReadOnlyCollection<Guid> catalogIds, CancellationToken ct);

    /// <summary>Las opciones de un catálogo a mano, por posición, con el aviso del texto repetido.</summary>
    Task<PagedResult<FormCatalogItemRow>> ListItemsAsync(Guid catalogId, ListFormCatalogItemsRequest request, CancellationToken ct);

    /// <summary>
    /// Un lote de opciones, activas y dadas de baja, por posición y después de <paramref name="afterPosition"/>: exportar y revisar
    /// un reemplazo leen en lotes, sin traer las 10.000 de una vez.
    /// </summary>
    Task<IReadOnlyList<FormCatalogStoredItemRow>> ListItemBatchAsync(Guid catalogId, int? afterPosition, int take, CancellationToken ct);

    /// <summary>
    /// Un lote de opciones activas, por posición y después de <paramref name="afterPosition"/>: lo que revisa qué no entra en
    /// WhatsApp (<c>presentationIssues</c>, E13), en lotes de 500.
    /// </summary>
    Task<IReadOnlyList<FormCatalogStoredItemRow>> ListActiveItemBatchAsync(Guid catalogId, int? afterPosition, int take, CancellationToken ct);

    /// <summary>Las opciones del catálogo con esos ids, sin las de otro: lo que vuelve a leer una propuesta guardada (ADR 0068 §9).</summary>
    Task<IReadOnlyList<FormCatalogStoredItemRow>> ListItemsByIdsAsync(Guid catalogId, IReadOnlyCollection<Guid> itemIds, CancellationToken ct);

    /// <summary>Las opciones del catálogo que tienen guardado alguno de esos códigos normalizados: lo que revisa lo pegado.</summary>
    Task<IReadOnlyList<FormCatalogStoredItemRow>> ListItemsByCodesAsync(Guid catalogId, IReadOnlyCollection<string> normalizedCodes,
        CancellationToken ct);

    /// <summary>
    /// Para cada búsqueda, las opciones cuyo código o texto la contienen, sin mirar mayúsculas ni acentos (como
    /// <see cref="ListItemsAsync"/>), por posición y hasta <paramref name="perSearch"/>: lo que resuelve, en una sola consulta,
    /// las opciones que nombra el asistente (§17).
    /// </summary>
    Task<IReadOnlyList<FormCatalogItemCandidateRow>> ListItemCandidatesAsync(Guid catalogId, IReadOnlyCollection<string> searches,
        int perSearch, CancellationToken ct);
}
