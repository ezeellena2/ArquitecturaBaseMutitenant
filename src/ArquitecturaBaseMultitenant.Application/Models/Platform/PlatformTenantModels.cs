using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

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

public sealed record CreatePlatformTenantRequest(string? Name, string? FirstCompanyName, Email OwnerEmail,
    string? Culture, string? TimeZoneId)
{
    public override string ToString() => nameof(CreatePlatformTenantRequest);
}

public sealed record CreatedPlatformTenantResponse(Guid Id, string Name, string Slug, Guid FirstCompanyId, Guid InvitationId);
public sealed record TenantProvisioningSnapshot(Guid? OwnerMemberId, Guid? PrimaryCompanyId);
