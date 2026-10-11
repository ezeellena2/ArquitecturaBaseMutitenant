using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auditing;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools.Business;

internal sealed class OrganizationAuditTool(AssistantAccessPolicy policy, IOrganizationAuditService audit) : AssistantTool(policy)
{
    public override string Key => "org_audit";
    public override Access? Access => Domain.Users.Access.Business;
    public override IReadOnlyList<string> Permissions => [Domain.Authorization.Permissions.Audit.Read];
    protected override IReadOnlyList<AssistantToolParameter> Parameters => [new("cursor", "string"), new("fromUtc", "utc"), new("toUtc", "utc")];
    protected override async Task<Result<AssistantToolData>> ExecuteCoreAsync(AssistantToolArguments args, CancellationToken ct)
    {
        var result = await audit.ListAsync(new ListOrganizationAuditRequest { After = args.Text("cursor"), Limit = 20, Period = "all", FromUtc = args.Instant("fromUtc"), ToUtc = args.Instant("toUtc") }, ct);
        if (result.IsFailure) return result.Error;
        var rows = result.Value.Items.Take(20).ToArray();
        return new AssistantQuickToolData(new AssistantAuditQuickDataResponse([.. rows.Select(row =>
            new AssistantAuditRowResponse(row.Id, row.Action, row.ActorDisplayName, row.OccurredAtUtc))], result.Value.NextCursor, result.Value.HasMore))
        {
            SubjectCopies = [.. rows.Where(row => row.ActorKind == nameof(Domain.Auditing.AuditActorKind.User)
                && row.ActorId != null && row.ActorDisplayName != null).Select(row =>
                new AssistantSnapshotSubjectCopy(row.ActorId!.Value, AssistantSnapshotCopyKind.AuditActorDisplayName, row.Id))],
        };
    }
}
