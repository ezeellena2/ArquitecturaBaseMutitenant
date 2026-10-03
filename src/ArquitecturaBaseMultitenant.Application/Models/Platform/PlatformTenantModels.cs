using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Models.Platform;

public sealed record ListPlatformTenantsRequest : PagedRequest
{
    public static readonly IReadOnlyCollection<string> SortableFields = ["name", "createdAtUtc"];
    public TenantStatus? Status { get; init; }
    public TenantRegistrationSource? Source { get; init; }
}

public sealed record PlatformTenantResponse(Guid Id, string Name, string? Slug, TenantStatus Status,
    TenantRegistrationSource Source, DateTime? CreatedAtUtc, int CompanyCount, int MemberCount, uint Version,
    string? OwnerName = null, string? OwnerEmail = null);

public sealed record PlatformTenantStatusCount(TenantStatus Status, int Count);
public sealed record PlatformTenantSourceCount(TenantRegistrationSource Source, int Count);
public sealed record PlatformTenantFilterCounts(int Total, IReadOnlyList<PlatformTenantStatusCount> Statuses,
    IReadOnlyList<PlatformTenantSourceCount> Sources);

public sealed record PlatformTenantStateRequest(uint? Version, string? Reason)
{
    public override string ToString() => nameof(PlatformTenantStateRequest);
}

public enum PlatformTenantAction { Reject, Suspend, Reactivate, Close }
