using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using CompanyMemberModelResponse = ArquitecturaBaseMultitenant.Application.Models.Companies.CompanyMemberResponse;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Organization;

public sealed record CompanyMemberListHttpQuery(int? Page, int? PageSize, string? Sort, string? Search,
    CompanyMemberState? State, Guid? RoleId, bool? WithoutRoles);

/// <summary>A quién se puede sumar a la empresa: se busca y se ordena solo por nombre (<c>displayName</c>).</summary>
public sealed record CompanyCandidateListHttpQuery(int? Page, int? PageSize, string? Sort, string? Search);

public sealed record AddCompanyMemberHttpRequest(Guid? MemberId, IReadOnlyCollection<Guid>? RoleIds);

public sealed record UpdateCompanyMemberRolesHttpRequest(IReadOnlyCollection<Guid>? RoleIds, uint? Version);

public sealed record ChangeCompanyMemberStatusHttpRequest(CompanyMembershipStatus? Status, uint? Version);

/// <summary>
/// Un miembro de la empresa. <c>InvitationId</c> y <c>InvitationExpiresAtUtc</c> son de la invitación pendiente que
/// incluye a esta empresa, la que se reenvía o revoca desde la ficha; si no hay, van en null.
/// </summary>
public sealed record CompanyMemberHttpResponse(Guid MemberId, Guid? UserId, string DisplayName, string? Email,
    CompanyMemberState State, CompanyMembershipStatus MembershipStatus,
    IReadOnlyList<CompanyMemberRoleResponse> Roles, uint Version, DateTime CreatedAtUtc, Guid? InvitationId,
    DateTime? InvitationExpiresAtUtc);

public static class CompanyMemberHttpMappings
{
    public static ListCompanyMembersRequest ToModel(this CompanyMemberListHttpQuery request) => new()
    {
        Page = request.Page ?? PagedRequest.DefaultPage,
        PageSize = request.PageSize ?? PagedRequest.DefaultPageSize,
        Sort = request.Sort,
        Search = request.Search,
        State = request.State,
        RoleId = request.RoleId,
        WithoutRoles = request.WithoutRoles,
    };

    public static ListCompanyCandidatesRequest ToModel(this CompanyCandidateListHttpQuery request) => new()
    {
        Page = request.Page ?? PagedRequest.DefaultPage,
        PageSize = request.PageSize ?? PagedRequest.DefaultPageSize,
        Sort = request.Sort,
        Search = request.Search,
    };

    public static CompanyMemberHttpResponse ToHttpResponse(this CompanyMemberModelResponse response) => new(
        response.MemberId, response.UserId, response.DisplayName, response.Email?.DisplayValue,
        response.State, response.MembershipStatus, response.Roles, response.Version, response.CreatedAtUtc,
        response.InvitationId, response.InvitationExpiresAtUtc);
}
