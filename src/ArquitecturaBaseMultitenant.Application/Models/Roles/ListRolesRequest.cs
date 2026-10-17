using ArquitecturaBaseMultitenant.Application.Common.Pagination;

namespace ArquitecturaBaseMultitenant.Application.Models.Roles;

/// <summary>Listado paginado de roles; Search busca por nombre o descripción.</summary>
public sealed record ListRolesRequest : PagedRequest
{
    public static readonly IReadOnlyCollection<string> SortableFields = ["name", "createdAtUtc"];

    public RoleScopeFilter? Scope { get; init; }
    public string? SystemRoleKey { get; init; }

    /// <summary>«Vale en» una empresa: sus roles propios y los que valen en cada empresa.</summary>
    public Guid? CompanyId { get; init; }
    public bool OnlyOwn { get; init; }
}
