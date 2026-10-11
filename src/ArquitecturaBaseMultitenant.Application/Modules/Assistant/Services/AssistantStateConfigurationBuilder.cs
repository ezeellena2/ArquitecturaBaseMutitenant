using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantStateConfigurationBuilder(IAssistantSettingsReader settings, IAssistantModelReader models,
    ITenantAdminReader owners, ITenantContext tenant, ICurrentUser user, AssistantAccessPolicy access)
{
    internal async Task<AssistantStateConfigurationRow> BuildAsync(CancellationToken ct)
    {
        var platform = await settings.FindPlatformAsync(ct);
        var business = user.Access == Access.Business;
        var organization = business ? await settings.FindOrganizationAsync(ct) : null;
        var mode = organization?.Mode ?? AssistantMode.PlatformKey;
        var effort = organization?.Effort ?? platform.Effort;
        var eligible = (await models.ListAllAsync(ct)).Where(model => AssistantModelCompatibilityPolicy.Supports(model, effort));
        var ownAllowed = true;
        var recipient = true;
        if (mode == AssistantMode.OwnKey)
        {
            ownAllowed = (await settings.FindPolicyAsync(tenant.RequiredTenantId, ct)).AllowOwnKey;
            recipient = (await owners.ListActiveVerifiedEmailRecipientsAsync(tenant.RequiredTenantId, ct)).Count > 0;
            if (!ownAllowed) eligible = [];
        }
        else eligible = eligible.Where(model => platform.AllowedModelIds.Contains(model.Id));
        var options = eligible.Select(model => new AssistantModelOptionResponse(model.Id, model.Name)).ToArray();
        var modelId = organization?.ModelId ?? platform.DefaultModelId;
        var hasKey = mode == AssistantMode.PlatformKey ? platform.HasKey : organization!.HasKey;
        var configurePermission = user.Access switch
        {
            Access.Business => Permissions.Assistant.Configure,
            Access.Platform => PlatformPermissions.Assistant.Configure,
            _ => null,
        };
        var canConfigure = configurePermission is not null
            && await access.CanUseAsync(user.Access, [configurePermission], null, ct);
        return new(hasKey && ownAllowed && recipient && options.Any(model => model.Id == modelId), canConfigure, mode, options,
            mode == AssistantMode.PlatformKey ? platform.DailyMessageLimit : organization!.DailyMessageLimit,
            business ? mode == AssistantMode.PlatformKey ? platform.MonthlyOrganizationBudget : organization!.MonthlyBudget : null,
            platform.RetentionDays);
    }
}
