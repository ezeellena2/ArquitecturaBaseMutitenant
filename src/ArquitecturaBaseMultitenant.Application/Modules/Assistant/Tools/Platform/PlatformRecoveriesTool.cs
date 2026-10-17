using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools.Platform;

internal sealed class PlatformRecoveriesTool(AssistantAccessPolicy policy, IPlatformRecoveryService recoveries) : AssistantTool(policy)
{
    public override string Key => "platform_recoveries";
    public override Access? Access => Domain.Users.Access.Platform;
    public override IReadOnlyList<string> Permissions => [PlatformPermissions.Recoveries.Manage];
    protected override IReadOnlyList<AssistantToolParameter> Parameters => [new("status", "string", true, ["Pending"]), Page, PageSize];
    protected override async Task<Result<AssistantToolData>> ExecuteCoreAsync(AssistantToolArguments args, CancellationToken ct)
    {
        var result = await recoveries.ListAsync(new ListPlatformRecoveriesRequest { Status = AccountRecoveryStatus.Pending, Page = args.Page, PageSize = args.PageSize }, ct);
        if (result.IsFailure) return result.Error;
        var rows = result.Value.Items.Take(AssistantTechnicalLimits.QuickRows).ToArray();
        return new AssistantQuickToolData(new AssistantRecoveriesQuickDataResponse([.. rows.Select(row =>
            new AssistantRecoveryRowResponse(row.Id, row.AccountName, row.Status.ToString(), row.CreatedAtUtc))], result.Value.TotalCount, result.Value.HasNext))
        {
            SubjectCopies = [.. rows.Where(row => row.AccountId != null && row.AccountName != null).Select(row =>
                new AssistantSnapshotSubjectCopy(row.AccountId!.Value, AssistantSnapshotCopyKind.RecoveryDisplayName, row.Id))],
        };
    }
}
