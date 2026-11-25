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
    ITenantAdminReader owners, ITenantContext tenant, ICurrentUser user, AssistantConnectionPolicy connection) : IAssistantConfigurationPolicy
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
        // 0064 §6: un proveedor guardado que esta versión no conoce pausa la conexión. Con la clave propia, el de esa
        // organización; con la de la plataforma, el del modelo por defecto, que es el de la clave: frena a todas.
        if (await connection.IsUnsupportedAsync(mode == AssistantMode.OwnKey ? modelId : platform.DefaultModelId, cancellationToken))
            return AssistantErrors.ProviderUnsupportedProvider;
        var model = (await models.ListAllAsync(cancellationToken)).SingleOrDefault(value => value.Id == modelId);
        var effort = organization?.Effort ?? platform.Effort;
        if (model is null || !AssistantModelCompatibilityPolicy.Supports(model, effort) || mode == AssistantMode.PlatformKey && !platform.AllowedModelIds.Contains(modelId))
            return AssistantErrors.ModelNotAllowed;
        if (!(mode == AssistantMode.PlatformKey ? platform.HasKey : organization!.HasKey)) return AssistantErrors.SettingsNotConfigured;
        // 0064 §7: antes de reservar se prueba descifrar la clave, sin el proveedor; una ilegible no gasta cupo.
        if (await connection.IsKeyUnreadableAsync(mode, cancellationToken)) return AssistantErrors.ProviderKeyUnreadable;
        var titleModel = (await models.ListAllAsync(cancellationToken))
            .Where(value => AssistantModelCompatibilityPolicy.Supports(value, AssistantEffort.Low)
                && (mode == AssistantMode.OwnKey || platform.AllowedModelIds.Contains(value.Id)))
            .OrderBy(value => value.InputPricePerMillion.Amount + value.OutputPricePerMillion.Amount)
            .ThenBy(value => value.Id).FirstOrDefault();
        var titlePrice = titleModel is null ? null : AssistantPriceSnapshotBuilder.Build(titleModel, AssistantModelReferenceBuilder.Build(titleModel));
        return new AssistantRuntimeConfiguration(mode, model, AssistantModelReferenceBuilder.Build(model), effort,
            mode == AssistantMode.PlatformKey ? platform.DailyMessageLimit : organization!.DailyMessageLimit,
            mode == AssistantMode.PlatformKey ? platform.MonthlyOrganizationBudget : organization!.MonthlyBudget,
            platform.RetentionDays, titlePrice);
    }
}
