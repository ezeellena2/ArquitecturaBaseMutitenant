using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools.Business;

internal sealed class RoleMembersTool(AssistantAccessPolicy policy, IRoleService roles, IOrganizationUserService users) : AssistantTool(policy)
{
    public override string Key => "org_role_members";
    public override Access? Access => Domain.Users.Access.Business;
    public override IReadOnlyList<string> Permissions => [Domain.Authorization.Permissions.Roles.Read, Domain.Authorization.Permissions.Users.Read];
    protected override IReadOnlyList<AssistantToolParameter> Parameters => [new("roleId", "uuid", true), Page, PageSize];
    protected override async Task<Result<AssistantToolData>> ExecuteCoreAsync(AssistantToolArguments args, CancellationToken ct)
    {
        var roleId = args.Id("roleId")!.Value;
        var role = await roles.GetAsync(roleId, ct);
        if (role.IsFailure) return role.Error;
        var result = await users.ListAsync(new ListOrganizationUsersRequest { RoleId = roleId, Page = args.Page, PageSize = args.PageSize }, ct);
        if (result.IsFailure) return result.Error;
        var rows = result.Value.Items.Take(20).ToArray();
        return new AssistantQuickToolData(new AssistantPeopleQuickDataResponse([.. rows.Select(row =>
            new AssistantPersonRowResponse(row.MemberId, row.DisplayName, row.State.ToString()))], result.Value.TotalCount, result.Value.HasNext))
        {
            SubjectCopies = [.. rows.Where(row => row.UserId != null && row.DisplayName != null).Select(row =>
                new AssistantSnapshotSubjectCopy(row.UserId!.Value, AssistantSnapshotCopyKind.PeopleDisplayName, row.MemberId))],
        };
    }
}
