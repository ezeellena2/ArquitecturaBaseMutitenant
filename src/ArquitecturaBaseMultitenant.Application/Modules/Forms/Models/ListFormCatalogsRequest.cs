using ArquitecturaBaseMultitenant.Application.Common.Pagination;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

/// <summary>El listado de catálogos; <see cref="PagedRequest.Search"/> busca por el nombre.</summary>
public sealed record ListFormCatalogsRequest : PagedRequest
{
    public static readonly IReadOnlyCollection<string> SortableFields = ["name", "source", "valueType", "optionsCount", "status"];

    public FormCatalogSourceFilter? Source { get; init; }
    public FormCatalogStatusFilter? Status { get; init; }
}

/// <summary>Las opciones de un catálogo a mano, por posición; <see cref="PagedRequest.Search"/> busca por el texto o el código.</summary>
public sealed record ListFormCatalogItemsRequest : PagedRequest
{
    public FormCatalogStatusFilter? Status { get; init; }
}
