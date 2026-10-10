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
        return result.IsFailure ? result.Error : new AssistantQuickToolData(new AssistantAuditQuickDataResponse([.. result.Value.Items.Take(20).Select(row => new AssistantAuditRowResponse(row.Id, row.Action, row.ActorDisplayName, row.OccurredAtUtc))], result.Value.NextCursor, result.Value.HasMore));
    }
}
