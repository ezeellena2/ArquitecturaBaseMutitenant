using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
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

/// <summary>
/// A quién se puede sumar a la empresa: las personas activas de la organización que todavía no están en ella. Se busca
/// y se ordena solo por nombre, porque el correo se muestra tapado.
/// </summary>
public sealed record ListCompanyCandidatesRequest : PagedRequest
{
    public static readonly IReadOnlyCollection<string> SortableFields = ["displayName"];
}

/// <summary>Una persona que se puede sumar a la empresa, con su correo verificado principal, si tiene.</summary>
public sealed record CompanyCandidateRow(Guid MemberId, string DisplayName, Email? Email);

/// <summary>
/// Lo que ve quien suma a alguien a su empresa: el nombre y el correo tapado (<c>PersonalDataMask</c>), nunca el
/// completo ni la cuenta. Sin nombre, el nombre es el correo tapado.
/// </summary>
public sealed record CompanyCandidateResponse(Guid MemberId, string DisplayName, string? MaskedEmailAddress);

public sealed record CompanyMemberRoleRow(Guid Id, string Name, bool IsSystemRole, RoleScope Scope, Guid? CompanyId);

/// <summary>Una empresa activa donde la persona es miembro activo, con sus roles en esa empresa.</summary>
public sealed record UserCompanyRow(Guid CompanyId, string CompanyName, IReadOnlyList<CompanyMemberRoleRow> Roles);

/// <summary>
/// Un miembro de la empresa; <c>State</c> lo calcula el reader en SQL (CompanyMemberStatePolicy). La invitación
/// pendiente viene solo si incluye a esta empresa (membresía activa): es la que se reenvía o revoca desde la ficha.
/// <c>InvitationOnlyForCompany</c> dice si, además, es únicamente de esta empresa (CompanyInvitationPolicy): sin roles
/// de la organización ni otra membresía activa. El servicio la muestra solo a quien puede resolverla.
/// </summary>
public sealed record CompanyMemberRow(Guid MemberId, Guid? UserId, string DisplayName, Email? Email,
    CompanyMemberState State, CompanyMembershipStatus MembershipStatus,
    IReadOnlyList<CompanyMemberRoleRow> Roles, uint Version, DateTime CreatedAtUtc,
    Guid? InvitationId, DateTime? InvitationExpiresAtUtc, bool InvitationOnlyForCompany);

public sealed record CompanyMemberResponse(Guid MemberId, Guid? UserId, string DisplayName, Email? Email,
    CompanyMemberState State, CompanyMembershipStatus MembershipStatus,
    IReadOnlyList<CompanyMemberRoleResponse> Roles, uint Version, DateTime CreatedAtUtc,
    Guid? InvitationId, DateTime? InvitationExpiresAtUtc);

public sealed record CompanyMemberRoleResponse(Guid Id, string Name, bool IsSystemRole, RoleScope Scope,
    Guid? CompanyId);

public sealed record CompanyMemberFilterCountsResponse(int All, int Active, int PendingDeletion,
    int Inactive, int Invited);

public sealed record AddCompanyMemberRequest(Guid CompanyId, Guid? MemberId, IReadOnlyCollection<Guid>? RoleIds);

public sealed record UpdateCompanyMemberRolesRequest(Guid CompanyId, Guid MemberId,
    IReadOnlyCollection<Guid>? RoleIds, uint? Version);

public sealed record ChangeCompanyMemberStatusRequest(Guid CompanyId, Guid MemberId,
    CompanyMembershipStatus? Status, uint? Version);
