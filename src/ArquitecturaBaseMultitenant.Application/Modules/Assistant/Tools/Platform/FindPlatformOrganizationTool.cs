using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools.Platform;

internal sealed class FindPlatformOrganizationTool(AssistantAccessPolicy policy, ITenantAdministrationService tenants) : AssistantTool(policy)
{
    public override string Key => "platform_find_organization";
    public override Access? Access => Domain.Users.Access.Platform;
    public override IReadOnlyList<string> Permissions => [PlatformPermissions.Tenants.Read];
    protected override IReadOnlyList<AssistantToolParameter> Parameters => [new("search", "string", true), Page, PageSize];
    protected override async Task<Result<AssistantToolData>> ExecuteCoreAsync(AssistantToolArguments args, CancellationToken ct)
    {
        var result = await tenants.ListAsync(new ListPlatformTenantsRequest { Search = args.Text("search"), Page = args.Page, PageSize = args.PageSize }, ct);
        return result.IsFailure ? result.Error : new AssistantQuickToolData(new AssistantOrganizationsQuickDataResponse([.. result.Value.Items.Take(AssistantTechnicalLimits.QuickRows).Select(row => new AssistantOrganizationRowResponse(row.Id, row.Name, row.Status.ToString()))], result.Value.TotalCount, result.Value.HasNext));
    }
}
