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
}
