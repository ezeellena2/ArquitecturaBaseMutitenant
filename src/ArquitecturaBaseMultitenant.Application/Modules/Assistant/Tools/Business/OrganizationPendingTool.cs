using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools.Business;

internal sealed class OrganizationPendingTool(AssistantAccessPolicy policy, IHomeService home) : AssistantTool(policy)
{
    public override string Key => "org_pending";
    public override Access? Access => Domain.Users.Access.Business;
    public override IReadOnlyList<string> Permissions => [];
    protected override IReadOnlyList<AssistantToolParameter> Parameters => [];
    protected override async Task<Result<AssistantToolData>> ExecuteCoreAsync(AssistantToolArguments args, CancellationToken ct)
    {
        var result = await home.GetAsync(ct);
        return result.IsFailure ? result.Error : new AssistantPendingToolData([.. result.Value.Pending.Take(20).Select(row => new AssistantPendingToolRow(row.Area, row.Kind, row.Subject, row.Urgent))], result.Value.Pending.Count > 20);
    }
}
