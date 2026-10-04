using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Api.Contracts.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using OrganizationUserModelResponse = ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers.OrganizationUserResponse;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Organization;

public sealed record OrganizationUserListHttpQuery(int? Page, int? PageSize, string? Sort, string? Search,
    OrganizationUserState? State, Guid? CompanyId, bool? WithoutCompany);

public sealed record InviteOrganizationUserHttpRequest(string? Email, IReadOnlyCollection<Guid>? RoleIds,
    IReadOnlyCollection<InviteCompanyRolesHttpRequest>? Companies)
{
    public override string ToString() => nameof(InviteOrganizationUserHttpRequest);
}

public sealed record InviteCompanyRolesHttpRequest(Guid CompanyId, IReadOnlyCollection<Guid>? RoleIds);

public sealed record UpdateOrganizationUserHttpRequest(IReadOnlyCollection<Guid>? RoleIds,
    IReadOnlyCollection<InviteCompanyRolesHttpRequest>? Companies, uint? Version, bool? IsActive = null);

public sealed record ChangeOrganizationUserStatusHttpRequest(bool? IsActive, uint? Version);

public sealed record OrganizationUserHttpResponse(Guid MemberId, Guid? UserId, string DisplayName, string? Email,
    OrganizationUserState State, bool HasManagedEmail, IReadOnlyList<OrganizationUserRoleRow> Roles,
    IReadOnlyList<OrganizationUserCompanyRow> Companies, Guid? InvitationId,
    DateTime? InvitationIssuedAtUtc, DateTime? InvitationExpiresAtUtc, uint Version, DateTime CreatedAtUtc,
    DateTime? DeletionScheduledForUtc);

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

    public static Result<InviteOrganizationUserRequest> ToModel(this InviteOrganizationUserHttpRequest request)
    {
        var email = AuthEmailMapping.Parse(request.Email);
        if (email.IsFailure) return email.Error;
        return new InviteOrganizationUserRequest(email.Value, request.RoleIds, request.Companies?.Select(company =>
            new InviteCompanyRolesRequest(company.CompanyId, company.RoleIds)).ToArray());
    }

    public static UpdateOrganizationUserRequest ToModel(this UpdateOrganizationUserHttpRequest request,
        Guid memberId) => new(memberId, request.RoleIds, request.Companies?.Select(company =>
            new InviteCompanyRolesRequest(company.CompanyId, company.RoleIds)).ToArray(), request.Version,
            request.IsActive);

    public static OrganizationUserHttpResponse ToHttpResponse(this OrganizationUserModelResponse response) => new(
        response.MemberId, response.UserId, response.DisplayName, response.Email?.DisplayValue, response.State,
        response.HasManagedEmail, response.Roles, response.Companies, response.InvitationId,
        response.InvitationIssuedAtUtc, response.InvitationExpiresAtUtc, response.Version, response.CreatedAtUtc,
        response.DeletionScheduledForUtc);
}
