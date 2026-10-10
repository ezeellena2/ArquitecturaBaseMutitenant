using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools.Personal;

internal sealed class MyOrganizationsTool(AssistantAccessPolicy policy, IProfileService profiles) : AssistantTool(policy)
{
    public override string Key => "my_organizations";
    public override Access? Access => Domain.Users.Access.Consumer;
    public override IReadOnlyList<string> Permissions => [];
    protected override IReadOnlyList<AssistantToolParameter> Parameters => [];
    protected override async Task<Result<AssistantToolData>> ExecuteCoreAsync(AssistantToolArguments args, CancellationToken ct)
    {
        var profile = await profiles.GetAsync(ct);
        if (profile.IsFailure) return profile.Error;
        var organizations = profile.Value.Organizations;
        return new AssistantQuickToolData(new AssistantOrganizationsQuickDataResponse(
            [.. organizations.OrderBy(row => row.Name, StringComparer.Ordinal).ThenBy(row => row.Id).Take(20)
                .Select(row => new AssistantOrganizationRowResponse(row.Id, row.Name, row.Status.ToString()))],
            organizations.Count, organizations.Count > 20));
    }
}
