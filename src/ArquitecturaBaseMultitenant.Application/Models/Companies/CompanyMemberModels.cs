using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Companies;

public enum CompanyMemberState
{
    Active,
    PendingDeletion,
    Inactive,
    Invited,
}

public sealed record ListCompanyMembersRequest : PagedRequest
{
    public static readonly IReadOnlyCollection<string> SortableFields = ["displayName", "email"];
    public CompanyMemberState? State { get; init; }
    public Guid? RoleId { get; init; }
    public bool? WithoutRoles { get; init; }
}

public sealed record CompanyMemberRoleRow(Guid Id, string Name, bool IsSystemRole, RoleScope Scope, Guid? CompanyId);

/// <summary>Una empresa activa donde la persona es miembro activo, con sus roles en esa empresa.</summary>
public sealed record UserCompanyRow(Guid CompanyId, string CompanyName, IReadOnlyList<CompanyMemberRoleRow> Roles);

public sealed record CompanyMemberRow(Guid MemberId, Guid? UserId, string DisplayName, Email? Email,
    MemberStatus MemberStatus, UserStatus? UserStatus, CompanyMembershipStatus MembershipStatus,
    IReadOnlyList<CompanyMemberRoleRow> Roles, uint Version, DateTime CreatedAtUtc);

public sealed record CompanyMemberResponse(Guid MemberId, Guid? UserId, string DisplayName, Email? Email,
    CompanyMemberState State, CompanyMembershipStatus MembershipStatus,
    IReadOnlyList<CompanyMemberRoleResponse> Roles, uint Version, DateTime CreatedAtUtc);

public sealed record CompanyMemberRoleResponse(Guid Id, string Name, bool IsSystemRole, RoleScope Scope,
    Guid? CompanyId);

public sealed record CompanyMemberFilterCountsResponse(int All, int Active, int PendingDeletion,
    int Inactive, int Invited);

public sealed record AddCompanyMemberRequest(Guid CompanyId, Guid? MemberId, IReadOnlyCollection<Guid>? RoleIds);

public sealed record UpdateCompanyMemberRolesRequest(Guid CompanyId, Guid MemberId,
    IReadOnlyCollection<Guid>? RoleIds, uint? Version);

public sealed record ChangeCompanyMemberStatusRequest(Guid CompanyId, Guid MemberId,
    CompanyMembershipStatus? Status, uint? Version);
