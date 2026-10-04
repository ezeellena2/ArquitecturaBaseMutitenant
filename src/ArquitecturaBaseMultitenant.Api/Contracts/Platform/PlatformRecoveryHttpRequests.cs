using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Platform;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Platform;

public sealed class PlatformRecoveriesQuery
{
    public int Page { get; init; } = PagedRequest.DefaultPage;
    public int PageSize { get; init; } = PagedRequest.DefaultPageSize;
    public string? Sort { get; init; }
    public string? Search { get; init; }
    public AccountRecoveryStatus? Status { get; init; }

    public ListPlatformRecoveriesRequest ToModel() => new()
    {
        Page = Page, PageSize = PageSize, Sort = Sort, Search = Search, Status = Status,
    };

    // La búsqueda puede ser un correo o un teléfono: nunca se registra.
    public override string ToString() => nameof(PlatformRecoveriesQuery);
}
