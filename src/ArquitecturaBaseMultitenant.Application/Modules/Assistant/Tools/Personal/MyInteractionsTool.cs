using ArquitecturaBaseMultitenant.Application.Models.Engagement;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools.Personal;

internal sealed class MyInteractionsTool(AssistantAccessPolicy policy, AssistantInteractionsBuilder interactions) : AssistantTool(policy)
{
    public override string Key => "my_interactions";
    public override Access? Access => Domain.Users.Access.Consumer;
    public override IReadOnlyList<string> Permissions => [];
    protected override IReadOnlyList<AssistantToolParameter> Parameters => [Page, PageSize];
    protected override async Task<Result<AssistantToolData>> ExecuteCoreAsync(AssistantToolArguments args, CancellationToken ct)
    {
        var result = await interactions.ListAsync(new ListConsumerInteractionsRequest { Page = args.Page, PageSize = args.PageSize }, ct);
        return result.IsFailure ? result.Error : new AssistantQuickToolData(new AssistantInteractionsQuickDataResponse(result.Value));
    }
}
