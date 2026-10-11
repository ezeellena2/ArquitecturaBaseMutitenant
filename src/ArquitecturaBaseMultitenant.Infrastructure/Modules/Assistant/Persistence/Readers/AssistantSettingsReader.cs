using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Configuration;
using Microsoft.Extensions.Options;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Readers;
internal sealed class AssistantSettingsReader(ApplicationDbContext context, ITenantContext tenant, IOptions<AssistantOptions> options) : IAssistantSettingsReader
{
    public Task<bool> IsPersonalEnabledAsync(CancellationToken ct) => context.Set<AssistantPlatformSettings>().AsNoTracking().AnyAsync(value => value.Id == AssistantPlatformSettings.SingletonId && value.PersonalEnabled, ct);
    public async Task<PlatformAssistantSettingsResponse> FindPlatformAsync(CancellationToken ct)
    {
        var row = await context.Set<AssistantPlatformSettings>().AsNoTracking().Where(value => value.Id == AssistantPlatformSettings.SingletonId)
            .Select(value => new PlatformAssistantSettingsResponse(value.DefaultModelId, value.AllowedModelIds, Enum.Parse<AssistantEffort>(value.Effort), value.DailyMessageLimit,
                value.MonthlyOrganizationBudget, value.PersonalEnabled, value.RetentionDays, value.EncryptedApiKey != null, value.MaskedKeySuffix, value.Version)).SingleOrDefaultAsync(ct);
        row ??= new PlatformAssistantSettingsResponse(Guid.Empty, [], AssistantEffort.Low, AssistantFeature.DefaultDailyMessageLimit,
            new Money(100m, CurrencyCode.Create(AssistantContractPolicy.BillingCurrency).Value), false, AssistantFeature.DefaultRetentionDays, false, null, 0);
        var overridden = await context.Set<AssistantPlatformSettings>().AsNoTracking()
            .Where(value => value.Id == AssistantPlatformSettings.SingletonId).Select(value => value.HasKeyOverride).SingleOrDefaultAsync(ct);
        var fallback = !overridden && !row.HasKey && !string.IsNullOrWhiteSpace(options.Value.Platform.ApiKey);
        return row with { HasKey = row.HasKey || fallback, MaskedKeySuffix = fallback ? Suffix(options.Value.Platform.ApiKey!) : row.MaskedKeySuffix };
    }
    public Task<AssistantOrganizationSettingsRow?> FindOrganizationAsync(CancellationToken ct)
    {
        var tenantId = tenant.RequiredTenantId;
        return context.Set<AssistantOrganizationSettings>().AsNoTracking().Where(value => value.TenantId == tenantId)
            .Select(value => new AssistantOrganizationSettingsRow(Enum.Parse<AssistantMode>(value.Mode), value.ModelId,
                Enum.Parse<AssistantEffort>(value.Effort), value.DailyMessageLimit, value.MonthlyBudget, value.RetentionDays,
                value.EncryptedApiKey != null, value.MaskedKeySuffix, value.Version)).SingleOrDefaultAsync(ct);
    }
    public async Task<AssistantPolicyResponse> FindPolicyAsync(Guid tenantId, CancellationToken ct) =>
        await context.Set<AssistantOrganizationPolicy>().AsNoTracking().Where(value => value.OrganizationId == tenantId)
            .Select(value => new AssistantPolicyResponse(value.AllowOwnKey, value.Version)).SingleOrDefaultAsync(ct) ?? new(false, 0);

    public async Task<AssistantKeyMaterial> GetPlatformKeyAsync(CancellationToken ct) =>
        await context.Set<AssistantPlatformSettings>().AsNoTracking().Where(value => value.Id == AssistantPlatformSettings.SingletonId)
            .Select(value => new AssistantKeyMaterial(value.EncryptedApiKey, value.MaskedKeySuffix, value.HasKeyOverride)).SingleOrDefaultAsync(ct) ?? new(null, null, false);

    public async Task<AssistantKeyMaterial> GetOrganizationKeyAsync(CancellationToken ct)
    {
        var tenantId = tenant.RequiredTenantId;
        return await context.Set<AssistantOrganizationSettings>().AsNoTracking().Where(value => value.TenantId == tenantId)
            .Select(value => new AssistantKeyMaterial(value.EncryptedApiKey, value.MaskedKeySuffix, true)).SingleOrDefaultAsync(ct) ?? new(null, null, true);
    }
    private static string Suffix(string key) => key[^Math.Min(4, key.Length)..];
}
