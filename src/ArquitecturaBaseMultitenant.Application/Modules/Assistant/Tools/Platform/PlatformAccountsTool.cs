using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools.Platform;

internal sealed class PlatformAccountsTool(AssistantAccessPolicy policy, IPlatformAccountService accounts) : AssistantTool(policy)
{
    public override string Key => "platform_accounts";
    public override Access? Access => Domain.Users.Access.Platform;
    public override IReadOnlyList<string> Permissions => [PlatformPermissions.Accounts.Read];
    protected override IReadOnlyList<AssistantToolParameter> Parameters => [new("status", "string", Values: Enum.GetNames<PlatformAccountState>()), new("search", "string"), Page, PageSize];
    protected override async Task<Result<AssistantToolData>> ExecuteCoreAsync(AssistantToolArguments args, CancellationToken ct)
    {
        var result = await accounts.ListAsync(new ListPlatformAccountsRequest { State = args.EnumValue<PlatformAccountState>("status"), Search = args.Text("search"), Page = args.Page, PageSize = args.PageSize }, ct);
        if (result.IsFailure) return result.Error;
        var rows = result.Value.Items.Take(AssistantTechnicalLimits.QuickRows).ToArray();
        return new AssistantQuickToolData(new AssistantPeopleQuickDataResponse([.. rows.Select(row =>
            new AssistantPersonRowResponse(row.Id, row.DisplayName, row.State.ToString()))], result.Value.TotalCount, result.Value.HasNext))
        {
            SubjectCopies = [.. rows.Where(row => row.DisplayName != null).Select(row =>
                new AssistantSnapshotSubjectCopy(row.Id, AssistantSnapshotCopyKind.PeopleDisplayName, row.Id))],
        };
    }
}
