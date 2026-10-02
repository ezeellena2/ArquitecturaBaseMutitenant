using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;

public enum OrganizationUserState
{
    Active,
    PendingDeletion,
    Invited,
    Expired,
    Inactive,
}

public sealed record ListOrganizationUsersRequest : PagedRequest
{
    public static readonly IReadOnlyCollection<string> SortableFields = ["displayName", "email", "createdAtUtc"];
    public OrganizationUserState? State { get; init; }
    public Guid? CompanyId { get; init; }
    public bool WithoutCompany { get; init; }
}

public sealed record OrganizationUserRoleRow(Guid Id, string Name, bool IsSystemRole, RoleScope Scope,
    Guid? CompanyId);

public sealed record OrganizationUserCompanyRow(Guid CompanyId, string CompanyName,
    CompanyMembershipStatus Status, IReadOnlyList<OrganizationUserRoleRow> Roles);

public sealed record OrganizationUserRow(Guid MemberId, Guid? UserId, string DisplayName, string? Email,
    bool HasManagedEmail, MemberStatus MemberStatus, UserStatus? UserStatus,
    InvitationStatus? InvitationStatus, Guid? InvitationId, DateTime? InvitationIssuedAtUtc,
    DateTime? InvitationExpiresAtUtc, IReadOnlyList<OrganizationUserRoleRow> Roles,
    IReadOnlyList<OrganizationUserCompanyRow> Companies, uint Version, DateTime CreatedAtUtc);

public sealed record OrganizationUserResponse(Guid MemberId, Guid? UserId, string DisplayName, string? Email,
    OrganizationUserState State, bool HasManagedEmail, IReadOnlyList<OrganizationUserRoleRow> Roles,
    IReadOnlyList<OrganizationUserCompanyRow> Companies, Guid? InvitationId,
    DateTime? InvitationIssuedAtUtc, DateTime? InvitationExpiresAtUtc, uint Version, DateTime CreatedAtUtc);

public sealed record OrganizationUserFilterCountsResponse(int All, int Active, int PendingDeletion,
    int Invited, int Expired, int Inactive);

public sealed record InviteOrganizationUserRequest(string? Email, IReadOnlyCollection<Guid>? RoleIds,
    IReadOnlyCollection<InviteCompanyRolesRequest>? Companies);

public sealed record InviteCompanyRolesRequest(Guid CompanyId, IReadOnlyCollection<Guid>? RoleIds);

public sealed record UpdateOrganizationUserRequest(Guid MemberId, IReadOnlyCollection<Guid>? RoleIds,
    IReadOnlyCollection<InviteCompanyRolesRequest>? Companies, uint? Version);

public sealed record ChangeOrganizationUserStatusRequest(Guid MemberId, bool? IsActive, uint? Version);
