using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Models.Platform;

public sealed record ListPlatformTenantsRequest : PagedRequest
{
    public static readonly IReadOnlyCollection<string> SortableFields = ["name", "createdAtUtc"];
    public TenantStatus? Status { get; init; }
    public TenantRegistrationSource? Source { get; init; }
}

public sealed record PlatformTenantResponse(Guid Id, string Name, string? Slug, TenantStatus Status,
    TenantRegistrationSource Source, DateTime? CreatedAtUtc, int CompanyCount, int MemberCount, uint Version,
    string? OwnerName = null, Email? OwnerEmail = null);

/// <summary>Ficha global; su versión pertenece únicamente a Tenants.</summary>
public sealed record PlatformTenantDetailResponse(Guid Id, string Name, string? Slug, TenantStatus Status,
    TenantRegistrationSource Source, DateTime? CreatedAtUtc, int CompanyCount, int MemberCount, uint Version,
    string? OwnerName, Email? OwnerEmail, IReadOnlyList<PlatformTenantOwnerResponse> Owners,
    PlatformTenantStateChangeResponse? LastStateChange)
{
    public override string ToString() => nameof(PlatformTenantDetailResponse);
}

public sealed record PlatformTenantOwnerResponse(Guid Id, string? DisplayName, Email? Email,
    UserStatus IdentityStatus, MemberStatus MembershipStatus, DateTime? LastSignInAtUtc,
    LoginAuditMethod? LastSignInMethod)
{
    public override string ToString() => nameof(PlatformTenantOwnerResponse);
}

public sealed record PlatformTenantStateChangeResponse(DateTime AtUtc, string Action, string? Reason)
{
    public override string ToString() => nameof(PlatformTenantStateChangeResponse);
}
public sealed record PlatformTenantStateChangeRow(DateTime AtUtc, SecurityEventType Type, string? Reason)
{
    public override string ToString() => nameof(PlatformTenantStateChangeRow);
}
public sealed record PlatformTenantDetailRow(PlatformTenantResponse Tenant,
    IReadOnlyList<PlatformTenantOwnerResponse> Owners, PlatformTenantStateChangeRow? LastStateChange,
    Email? PendingOwnerEmail = null)
{
    public override string ToString() => nameof(PlatformTenantDetailRow);
}

public sealed record ListPlatformTenantHistoryRequest : CursorRequest;
public sealed record PlatformTenantHistoryResponse(Guid Id, DateTime OccurredAtUtc, string Action,
    string Actor, string ActorRole, string? Reason, string Summary)
{
    public override string ToString() => nameof(PlatformTenantHistoryResponse);
}

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
