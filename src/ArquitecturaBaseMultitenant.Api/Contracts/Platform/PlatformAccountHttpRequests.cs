using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Platform;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Platform;

/// <summary>Suspender, reactivar, cerrar sesiones y dar de baja una cuenta: solo el motivo, sin versión.</summary>
public sealed record PlatformAccountReasonHttpRequest(string? Reason)
{
    public PlatformAccountReasonRequest ToModel() => new(Reason);
    public override string ToString() => nameof(PlatformAccountReasonHttpRequest);
}

/// <summary>Decidir un pedido de recuperación o quitar a un operador: la versión vista de esa fila y el motivo.</summary>
public sealed record PlatformAccountActionHttpRequest(uint? Version, string? Reason)
{
    public PlatformAccountActionRequest ToModel() => new(Version, Reason);
    public override string ToString() => nameof(PlatformAccountActionHttpRequest);
}

public sealed class PlatformAccountsQuery
{
    public int Page { get; init; } = PagedRequest.DefaultPage;
    public int PageSize { get; init; } = PagedRequest.DefaultPageSize;
    public string? Sort { get; init; }
    public string? Search { get; init; }
    public PlatformAccountState? State { get; init; }
    public PlatformAccountKind? Kind { get; init; }

    public ListPlatformAccountsRequest ToModel() => new()
    {
        Page = Page, PageSize = PageSize, Sort = Sort, Search = Search, State = State, Kind = Kind,
    };

    // La búsqueda puede ser un correo o un teléfono: nunca se registra.
    public override string ToString() => nameof(PlatformAccountsQuery);
}
