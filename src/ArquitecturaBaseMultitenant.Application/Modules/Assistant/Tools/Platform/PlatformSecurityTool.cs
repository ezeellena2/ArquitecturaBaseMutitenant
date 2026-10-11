using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools.Platform;

internal sealed class PlatformSecurityTool(AssistantAccessPolicy policy, IPlatformAuditService audit) : AssistantTool(policy)
{
    public override string Key => "platform_security";
    public override Access? Access => Domain.Users.Access.Platform;
    public override IReadOnlyList<string> Permissions => [PlatformPermissions.Audit.Read];
    protected override IReadOnlyList<AssistantToolParameter> Parameters => [new("cursor", "string"), new("fromUtc", "utc"), new("toUtc", "utc")];
    protected override async Task<Result<AssistantToolData>> ExecuteCoreAsync(AssistantToolArguments args, CancellationToken ct)
    {
        var result = await audit.ListAsync(new ListPlatformAuditRequest { After = args.Text("cursor"), Limit = 20, Period = "all", FromUtc = args.Instant("fromUtc"), ToUtc = args.Instant("toUtc") }, ct);
        if (result.IsFailure) return result.Error;
        var rows = result.Value.Items.Take(20).ToArray();
        return new AssistantQuickToolData(new AssistantSecurityQuickDataResponse([.. rows.Select(row =>
            new AssistantSecurityRowResponse(row.Id, row.Action, row.Actor, row.OccurredAtUtc))], result.Value.NextCursor, result.Value.HasMore))
        {
            SubjectCopies = [.. rows.Where(row => row.AuditActorId != null).Select(row =>
                new AssistantSnapshotSubjectCopy(row.AuditActorId!.Value, AssistantSnapshotCopyKind.SecurityActorDisplayName, row.Id))],
        };
    }
}
