using ArquitecturaBaseMultitenant.Application.Common.Pagination;

namespace ArquitecturaBaseMultitenant.Application.Models.PublicSite;

/// <summary>Listado de organizaciones publicadas; la búsqueda alcanza únicamente el nombre de la página.</summary>
public sealed record ListDirectoryRequest : PagedRequest
{
    public static readonly IReadOnlyCollection<string> SortableFields = ["name"];
}
