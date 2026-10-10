using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantConfigurationPolicy(IAssistantSettingsReader settings, IAssistantModelReader models,
    ITenantAdminReader owners, ITenantContext tenant, ICurrentUser user) : IAssistantConfigurationPolicy
{
    public async Task<Result<AssistantRuntimeConfiguration>> ResolveAsync(CancellationToken cancellationToken)
    {
        if (user.UserId is null || user.Access is null) return AssistantErrors.SettingsNotConfigured;
        var platform = await settings.FindPlatformAsync(cancellationToken);
        var organization = user.Access == Access.Business ? await settings.FindOrganizationAsync(cancellationToken) : null;
        var mode = organization?.Mode ?? AssistantMode.PlatformKey;
        if (mode == AssistantMode.OwnKey)
        {
            var id = tenant.RequiredTenantId;
            if (!(await settings.FindPolicyAsync(id, cancellationToken)).AllowOwnKey) return AssistantErrors.SettingsOwnKeyNotAllowed;
            if ((await owners.ListActiveVerifiedEmailRecipientsAsync(id, cancellationToken)).Count == 0) return AssistantErrors.SettingsAlertRecipientRequired;
        }
        var modelId = organization?.ModelId ?? platform.DefaultModelId;
        var model = (await models.ListAllAsync(cancellationToken)).SingleOrDefault(value => value.Id == modelId);
        var effort = organization?.Effort ?? platform.Effort;
        if (model is null || !AssistantModelCompatibilityPolicy.Supports(model, effort) || mode == AssistantMode.PlatformKey && !platform.AllowedModelIds.Contains(modelId))
            return AssistantErrors.ModelNotAllowed;
        if (!(mode == AssistantMode.PlatformKey ? platform.HasKey : organization!.HasKey)) return AssistantErrors.SettingsNotConfigured;
        return new AssistantRuntimeConfiguration(mode, model, AssistantModelReferenceBuilder.Build(model), effort,
            mode == AssistantMode.PlatformKey ? platform.DailyMessageLimit : organization!.DailyMessageLimit,
            mode == AssistantMode.PlatformKey ? platform.MonthlyOrganizationBudget : organization!.MonthlyBudget,
            platform.RetentionDays);
    }
}
