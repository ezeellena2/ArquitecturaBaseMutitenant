using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Platform;

public sealed class PlatformTenantsQuery
{
    public int Page { get; init; } = PagedRequest.DefaultPage;
    public int PageSize { get; init; } = PagedRequest.DefaultPageSize;
    public string? Sort { get; init; }
    public string? Search { get; init; }
    public TenantStatus? Status { get; init; }
    public TenantRegistrationSource? Source { get; init; }
    public ListPlatformTenantsRequest ToModel() => new()
    {
        Page = Page, PageSize = PageSize, Sort = Sort, Search = Search, Status = Status, Source = Source,
    };
    public override string ToString() => nameof(PlatformTenantsQuery);
}

public sealed record PlatformTenantStateHttpRequest(uint? Version, string? Reason)
{
    public PlatformTenantStateRequest ToModel() => new(Version, Reason);
    public override string ToString() => nameof(PlatformTenantStateHttpRequest);
}
