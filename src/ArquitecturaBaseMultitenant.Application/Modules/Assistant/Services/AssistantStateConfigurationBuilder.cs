using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Services.Features;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantStateConfigurationBuilder(IAssistantSettingsReader settings, IAssistantModelReader models,
    ITenantAdminReader owners, ITenantContext tenant, ICurrentUser user, AssistantAccessPolicy access,
    AssistantConnectionPolicy connection, ModuleLimitResolver limits)
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
        // 0064 §6 y §7: con el proveedor desconocido o la clave ilegible, el asistente no está disponible.
        var connected = hasKey && !await connection.IsUnsupportedAsync(mode == AssistantMode.OwnKey ? modelId : platform.DefaultModelId, ct)
            && !await connection.IsKeyUnreadableAsync(mode, ct);
        var configurePermission = user.Access switch
        {
            Access.Business => Permissions.Assistant.Configure,
            Access.Platform => PlatformPermissions.Assistant.Configure,
            _ => null,
        };
        var canConfigure = configurePermission is not null
            && await access.CanUseAsync(user.Access, [configurePermission], null, ct);
        // 0069 §4: con la clave de la plataforma, los topes salen del resolvedor con la organización del claim (nula para una
        // persona o Plataforma, que no tienen presupuesto del mes); con clave propia, los de la organización.
        var organizationId = business ? tenant.TenantId : null;
        var (dailyLimit, budget) = mode == AssistantMode.PlatformKey
            ? ((await limits.ResolveAsync(AssistantModuleLimitContributor.DailyMessagesKey, organizationId, ct)).Effective(null),
                business ? (await limits.ResolveAsync(AssistantModuleLimitContributor.MonthlyBudgetKey, organizationId, ct)).Amount : null)
            : (organization!.DailyMessageLimit, organization!.MonthlyBudget);
        return new(connected && ownAllowed && recipient && options.Any(model => model.Id == modelId), canConfigure, mode, options,
            dailyLimit, budget, platform.RetentionDays);
    }
}
