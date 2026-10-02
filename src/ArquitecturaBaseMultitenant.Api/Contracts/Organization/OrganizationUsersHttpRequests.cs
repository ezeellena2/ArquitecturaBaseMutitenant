using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Organization;

public sealed record OrganizationUserListHttpQuery(int? Page, int? PageSize, string? Sort, string? Search,
    OrganizationUserState? State, Guid? CompanyId, bool? WithoutCompany);

public sealed record InviteOrganizationUserHttpRequest(string? Email, IReadOnlyCollection<Guid>? RoleIds,
    IReadOnlyCollection<InviteCompanyRolesHttpRequest>? Companies);

public sealed record InviteCompanyRolesHttpRequest(Guid CompanyId, IReadOnlyCollection<Guid>? RoleIds);

public sealed record UpdateOrganizationUserHttpRequest(IReadOnlyCollection<Guid>? RoleIds,
    IReadOnlyCollection<InviteCompanyRolesHttpRequest>? Companies, uint? Version);

public sealed record ChangeOrganizationUserStatusHttpRequest(bool? IsActive, uint? Version);

public static class OrganizationUserHttpMappings
{
    public static ListOrganizationUsersRequest ToModel(this OrganizationUserListHttpQuery request) => new()
    {
        Page = request.Page ?? PagedRequest.DefaultPage,
        PageSize = request.PageSize ?? PagedRequest.DefaultPageSize,
        Sort = request.Sort,
        Search = request.Search,
        State = request.State,
        CompanyId = request.CompanyId,
        WithoutCompany = request.WithoutCompany ?? false,
    };

    public static InviteOrganizationUserRequest ToModel(this InviteOrganizationUserHttpRequest request) =>
        new(request.Email, request.RoleIds, request.Companies?.Select(company =>
            new InviteCompanyRolesRequest(company.CompanyId, company.RoleIds)).ToArray());

    public static UpdateOrganizationUserRequest ToModel(this UpdateOrganizationUserHttpRequest request,
        Guid memberId) => new(memberId, request.RoleIds, request.Companies?.Select(company =>
            new InviteCompanyRolesRequest(company.CompanyId, company.RoleIds)).ToArray(), request.Version);
}
