using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Platform;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Platform;

public sealed class PlatformTenantHistoryQuery
{
    public string? After { get; init; }
    public int Limit { get; init; } = CursorRequest.DefaultLimit;
    public ListPlatformTenantHistoryRequest ToModel() => new() { After = After, Limit = Limit };
}
