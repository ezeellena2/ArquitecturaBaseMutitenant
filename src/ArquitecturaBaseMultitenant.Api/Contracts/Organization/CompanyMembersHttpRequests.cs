using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using CompanyMemberModelResponse = ArquitecturaBaseMultitenant.Application.Models.Companies.CompanyMemberResponse;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Organization;

public sealed record CompanyMemberListHttpQuery(int? Page, int? PageSize, string? Sort, string? Search,
    CompanyMemberState? State, Guid? RoleId, bool? WithoutRoles);

public sealed record AddCompanyMemberHttpRequest(Guid? MemberId, IReadOnlyCollection<Guid>? RoleIds);

public sealed record UpdateCompanyMemberRolesHttpRequest(IReadOnlyCollection<Guid>? RoleIds, uint? Version);

public sealed record ChangeCompanyMemberStatusHttpRequest(CompanyMembershipStatus? Status, uint? Version);

public sealed record CompanyMemberResponse(Guid MemberId, Guid? UserId, string DisplayName, string? Email,
    CompanyMemberState State, CompanyMembershipStatus MembershipStatus,
    IReadOnlyList<CompanyMemberRoleResponse> Roles, uint Version, DateTime CreatedAtUtc);

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

    public static CompanyMemberResponse ToHttpResponse(this CompanyMemberModelResponse response) => new(
        response.MemberId, response.UserId, response.DisplayName, response.Email?.DisplayValue,
        response.State, response.MembershipStatus, response.Roles, response.Version, response.CreatedAtUtc);
}
