using ArquitecturaBaseMultitenant.Application.Common.Pagination;

namespace ArquitecturaBaseMultitenant.Application.Models.Auditing;

/// <summary>Search busca una opción de «Quién»; AuditSearch conserva la búsqueda del historial.</summary>
public sealed record ListOrganizationAuditActorsRequest : PagedRequest
{
    public static IReadOnlyCollection<string> SortableFields { get; } = ["name"];
    public string? Period { get; init; } = "30";
    public string? Category { get; init; }
    public string? AuditSearch { get; init; }

    public ListOrganizationAuditRequest EventFilters() => new() { Period = Period, Category = Category, Search = AuditSearch };
    public override string ToString() => nameof(ListOrganizationAuditActorsRequest);
}
