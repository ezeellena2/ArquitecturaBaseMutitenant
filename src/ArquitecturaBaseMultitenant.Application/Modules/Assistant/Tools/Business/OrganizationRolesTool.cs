using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools.Business;

internal sealed class OrganizationRolesTool(AssistantAccessPolicy policy, IRoleService roles) : AssistantTool(policy)
{
    public override string Key => "org_roles";
    public override Access? Access => Domain.Users.Access.Business;
    public override IReadOnlyList<string> Permissions => [Domain.Authorization.Permissions.Roles.Read];
    protected override IReadOnlyList<AssistantToolParameter> Parameters => [new("roleId", "uuid"), new("companyId", "uuid"), Page, PageSize];
    protected override async Task<Result<AssistantToolData>> ExecuteCoreAsync(AssistantToolArguments args, CancellationToken ct)
    {
        if (args.Id("roleId") is { } id)
        {
            var result = await roles.GetAsync(id, ct);
            if (result.IsFailure) return result.Error;
            if (args.Id("companyId") is { } company && (result.Value.Scope == RoleScope.Organization
                || result.Value.Scope == RoleScope.SpecificCompany && result.Value.CompanyId != company))
                return RoleErrors.NotFound;
            if (args.Id("companyId") is { } requestedCompany)
            {
                var page = await roles.ListForCompanyAsync(requestedCompany, new ListRolesRequest { Page = 1, PageSize = 20 }, ct);
                if (page.IsFailure) return page.Error;
            }
            return new AssistantRolesToolData([Row(result.Value)], 1, false);
        }
        var request = new ListRolesRequest { CompanyId = args.Id("companyId"), Page = args.Page, PageSize = args.PageSize };
        var list = args.Id("companyId") is { } companyId
            ? await roles.ListForCompanyAsync(companyId, request, ct) : await roles.ListAsync(request, ct);
        return list.IsFailure ? list.Error : new AssistantRolesToolData([.. list.Value.Items.Take(20).Select(Row)], list.Value.TotalCount, list.Value.HasNext);
    }
    private static AssistantRoleToolRow Row(RoleResponse role) => new(role.Id, role.Name, role.Scope.ToString(), role.CompanyId, role.Permissions);
}
