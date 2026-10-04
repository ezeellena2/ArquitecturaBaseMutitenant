using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Platform;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Platform;

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
