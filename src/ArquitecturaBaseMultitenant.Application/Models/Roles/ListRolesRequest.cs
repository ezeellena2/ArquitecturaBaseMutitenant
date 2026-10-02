using ArquitecturaBaseMultitenant.Application.Common.Pagination;

namespace ArquitecturaBaseMultitenant.Application.Models.Roles;

/// <summary>Listado paginado de roles; Search busca por nombre o descripción.</summary>
public sealed record ListRolesRequest : PagedRequest
{
    public static readonly IReadOnlyCollection<string> SortableFields = ["name", "createdAtUtc"];

    public RoleScopeFilter? Scope { get; init; }
}
