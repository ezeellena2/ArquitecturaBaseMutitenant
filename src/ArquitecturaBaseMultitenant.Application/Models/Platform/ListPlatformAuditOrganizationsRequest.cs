using ArquitecturaBaseMultitenant.Application.Common.Pagination;

namespace ArquitecturaBaseMultitenant.Application.Models.Platform;

/// <summary>Opciones paginadas: Search busca una organización; AuditSearch conserva la búsqueda del historial.</summary>
public sealed record ListPlatformAuditOrganizationsRequest : PagedRequest
{
    public static IReadOnlyCollection<string> SortableFields { get; } = ["name"];
    public string? Period { get; init; } = "30";
    public string? Type { get; init; }
    public Guid? AccountId { get; init; }
    public string? AuditSearch { get; init; }

    public ListPlatformAuditRequest EventFilters() => new() { Period = Period, Type = Type, AccountId = AccountId, Search = AuditSearch };
    public override string ToString() => nameof(ListPlatformAuditOrganizationsRequest);
}
