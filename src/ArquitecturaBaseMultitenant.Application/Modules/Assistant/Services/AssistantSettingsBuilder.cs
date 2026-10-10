using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantSettingsBuilder(IAssistantSettingsReader settings, IAssistantModelReader models,
    ITenantAdminReader owners, ITenantContext tenant)
{
    internal async Task<OrganizationAssistantSettingsResponse> BuildAsync(CancellationToken ct)
    {
        var tenantId = tenant.RequiredTenantId;
        var platform = await settings.FindPlatformAsync(ct);
        var organization = await settings.FindOrganizationAsync(ct);
        var policy = await settings.FindPolicyAsync(tenantId, ct);
        var compatible = (await models.ListAllAsync(ct)).Where(model => AssistantModelCompatibilityPolicy.IsCompatible(model)).ToArray();
        var platformModels = compatible.Where(value => platform.AllowedModelIds.Contains(value.Id)).ToArray();
        var ownModels = policy.AllowOwnKey ? compatible : [];
        var mode = organization?.Mode ?? AssistantMode.PlatformKey;
        return new OrganizationAssistantSettingsResponse(mode, organization?.ModelId ?? platform.DefaultModelId,
            organization?.Effort ?? platform.Effort,
            mode == AssistantMode.PlatformKey ? platform.DailyMessageLimit : organization!.DailyMessageLimit,
            mode == AssistantMode.PlatformKey ? platform.MonthlyOrganizationBudget : organization!.MonthlyBudget,
            policy.AllowOwnKey, (await owners.ListActiveVerifiedEmailRecipientsAsync(tenantId, ct)).Count > 0,
            platform.RetentionDays,
            mode == AssistantMode.PlatformKey ? platform.HasKey : organization!.HasKey,
            mode == AssistantMode.PlatformKey ? platform.MaskedKeySuffix : organization!.MaskedKeySuffix,
            organization?.Version ?? 0, platformModels, ownModels);
    }
}
