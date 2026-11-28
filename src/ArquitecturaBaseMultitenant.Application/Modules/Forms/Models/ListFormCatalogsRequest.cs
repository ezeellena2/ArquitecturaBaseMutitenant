using ArquitecturaBaseMultitenant.Application.Common.Pagination;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

/// <summary>El listado de catálogos; <see cref="PagedRequest.Search"/> busca por el nombre.</summary>
public sealed record ListFormCatalogsRequest : PagedRequest
{
    public static readonly IReadOnlyCollection<string> SortableFields = ["name", "source", "valueType", "optionsCount", "status"];

    public FormCatalogSourceFilter? Source { get; init; }
    public FormCatalogStatusFilter? Status { get; init; }
}

/// <summary>
/// Las opciones de un catálogo a mano, por posición; <see cref="PagedRequest.Search"/> busca por el texto o el código, e
/// <see cref="Ids"/> (E13, hasta 100 y sin vacíos) trae solo esas, para nombrar las opciones de una fuente.
/// </summary>
public sealed record ListFormCatalogItemsRequest : PagedRequest
{
    public FormCatalogStatusFilter? Status { get; init; }
    public IReadOnlyList<Guid>? Ids { get; init; }
}

/// <summary>
/// Una lista de las fuentes: roles, empresas, etiquetas y personas o clientes de ejemplo. <see cref="PagedRequest.Search"/> busca
/// por el nombre, y el orden es siempre por nombre.
/// </summary>
public sealed record ListFormCatalogLookupsRequest : PagedRequest
{
    public static readonly IReadOnlyCollection<string> SortableFields = ["name"];
}
