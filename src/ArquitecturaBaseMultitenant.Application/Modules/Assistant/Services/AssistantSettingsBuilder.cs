using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Services.Features;
using ArquitecturaBaseMultitenant.Domain.Assistant;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantSettingsBuilder(IAssistantSettingsReader settings, IAssistantModelReader models,
    ITenantAdminReader owners, ITenantContext tenant, AssistantConnectionPolicy connection, ModuleLimitResolver limits)
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
        // 0064 §6 y §7: cada uno ve lo que arregla. Con la clave propia, el Dueño ve la falla de su clave y su proveedor; con la
        // de la plataforma, la organización sabe solo que lo está resolviendo la plataforma.
        var (issue, unsupported) = mode == AssistantMode.OwnKey
            ? (await connection.IsKeyUnreadableAsync(AssistantMode.OwnKey, ct) ? AssistantErrors.ProviderKeyUnreadableCode : organization!.ProviderIssueCode,
                await connection.IsUnsupportedAsync(organization!.ModelId, ct))
            : (await PlatformIssueAsync(platform, ct) is null ? null : AssistantErrors.ProviderPlatformUnavailableCode, false);
        // 0069 §4: con la clave de la plataforma, los topes de la organización del claim salen del resolvedor.
        var (dailyLimit, budget) = mode == AssistantMode.PlatformKey
            ? ((await limits.ResolveAsync(AssistantModuleLimitContributor.DailyMessagesKey, tenantId, ct)).Effective(null),
                (await limits.ResolveAsync(AssistantModuleLimitContributor.MonthlyBudgetKey, tenantId, ct)).Amount)
            : (organization!.DailyMessageLimit, organization!.MonthlyBudget);
        return new OrganizationAssistantSettingsResponse(mode, organization?.ModelId ?? platform.DefaultModelId,
            organization?.Effort ?? platform.Effort, dailyLimit, budget,
            policy.AllowOwnKey, (await owners.ListActiveVerifiedEmailRecipientsAsync(tenantId, ct)).Count > 0,
            platform.RetentionDays,
            mode == AssistantMode.PlatformKey ? platform.HasKey : organization!.HasKey,
            // The platform key belongs to Platform: an organization only learns whether it is configured.
            mode == AssistantMode.PlatformKey ? null : organization!.MaskedKeySuffix,
            organization?.Version ?? 0, platformModels, ownModels) { ProviderIssue = issue, UnsupportedProvider = unsupported };
    }

    /// <summary>La de Plataforma: la falla de su clave, ilegible o la última que dijo el proveedor, y su proveedor.</summary>
    internal async Task<PlatformAssistantSettingsResponse> BuildPlatformAsync(CancellationToken ct)
    {
        var platform = await settings.FindPlatformAsync(ct);
        return platform with
        {
            ProviderIssue = await KeyIssueAsync(platform, ct),
            UnsupportedProvider = await connection.IsUnsupportedAsync(platform.DefaultModelId, ct),
        };
    }

    private async Task<string?> KeyIssueAsync(PlatformAssistantSettingsResponse platform, CancellationToken ct) =>
        await connection.IsKeyUnreadableAsync(AssistantMode.PlatformKey, ct) ? AssistantErrors.ProviderKeyUnreadableCode : platform.ProviderIssue;

    private async Task<string?> PlatformIssueAsync(PlatformAssistantSettingsResponse platform, CancellationToken ct) =>
        await connection.IsUnsupportedAsync(platform.DefaultModelId, ct) ? AssistantErrors.ProviderUnsupportedProviderCode : await KeyIssueAsync(platform, ct);
}
