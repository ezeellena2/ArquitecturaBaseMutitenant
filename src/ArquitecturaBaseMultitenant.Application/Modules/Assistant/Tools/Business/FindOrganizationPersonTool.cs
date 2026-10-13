using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools.Business;

internal sealed class FindOrganizationPersonTool(AssistantAccessPolicy policy, IOrganizationUserService users) : AssistantTool(policy)
{
    public override string Key => "org_find_person";
    public override Access? Access => Domain.Users.Access.Business;
    public override IReadOnlyList<string> Permissions => [Domain.Authorization.Permissions.Users.Read];
    protected override IReadOnlyList<AssistantToolParameter> Parameters => [new("search", "string", true), Page, PageSize];
    protected override async Task<Result<AssistantToolData>> ExecuteCoreAsync(AssistantToolArguments args, CancellationToken ct)
    {
        var result = await users.ListAsync(new ListOrganizationUsersRequest { Search = args.Text("search"), Page = args.Page, PageSize = args.PageSize }, ct);
        if (result.IsFailure) return result.Error;
        var rows = result.Value.Items.Take(AssistantTechnicalLimits.QuickRows).ToArray();
        return new AssistantQuickToolData(new AssistantPeopleQuickDataResponse([.. rows.Select(row =>
            new AssistantPersonRowResponse(row.MemberId, row.DisplayName, row.State.ToString()))], result.Value.TotalCount, result.Value.HasNext))
        {
            SubjectCopies = [.. rows.Where(row => row.UserId != null && row.DisplayName != null).Select(row =>
                new AssistantSnapshotSubjectCopy(row.UserId!.Value, AssistantSnapshotCopyKind.PeopleDisplayName, row.MemberId))],
        };
    }
}
