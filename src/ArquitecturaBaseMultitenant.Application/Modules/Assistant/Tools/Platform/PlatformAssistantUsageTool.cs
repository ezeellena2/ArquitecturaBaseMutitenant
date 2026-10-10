using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools.Platform;

internal sealed class PlatformAssistantUsageTool(AssistantAccessPolicy policy, IAssistantUsageService usage) : AssistantTool(policy)
{
    public override string Key => "platform_assistant_usage";
    public override Access? Access => Domain.Users.Access.Platform;
    public override IReadOnlyList<string> Permissions => [PlatformPermissions.Assistant.Read];
    protected override IReadOnlyList<AssistantToolParameter> Parameters => [new("month", "month", true), Page, PageSize];
    protected override async Task<Result<AssistantToolData>> ExecuteCoreAsync(AssistantToolArguments args, CancellationToken ct)
    {
        var result = await usage.ListPlatformAsync(new PlatformAssistantUsageRequest(args.Text("month"), args.Page, args.PageSize), ct);
        return result.IsFailure ? result.Error : new AssistantQuickToolData(new AssistantUsageQuickDataResponse(result.Value));
    }
}
