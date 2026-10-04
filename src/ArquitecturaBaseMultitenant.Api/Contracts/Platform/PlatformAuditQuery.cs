using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Platform;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Platform;

public sealed class PlatformAuditQuery
{
    public string? After { get; init; }
    public int Limit { get; init; } = CursorRequest.DefaultLimit;
    public string? Period { get; init; } = "30";
    public string? Type { get; init; }
    public Guid? TenantId { get; init; }
    public Guid? AccountId { get; init; }
    public string? Search { get; init; }

    public ListPlatformAuditRequest ToModel() => new()
    {
        After = After, Limit = Limit, Period = Period, Type = Type, TenantId = TenantId, AccountId = AccountId,
        Search = Search,
    };

    // La búsqueda puede tener un nombre: nunca se registra.
    public override string ToString() => nameof(PlatformAuditQuery);
}
