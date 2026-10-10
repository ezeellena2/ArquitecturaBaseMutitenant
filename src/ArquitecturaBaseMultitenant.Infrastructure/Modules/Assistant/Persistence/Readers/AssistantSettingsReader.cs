using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Readers;
internal sealed class AssistantSettingsReader(ApplicationDbContext context) : IAssistantSettingsReader
{
    public Task<bool> IsPersonalEnabledAsync(CancellationToken ct) => context.Set<AssistantPlatformSettings>().AsNoTracking().AnyAsync(value => value.Id == AssistantPlatformSettings.SingletonId && value.PersonalEnabled, ct);
    public async Task<PlatformAssistantSettingsResponse> FindPlatformAsync(CancellationToken ct)
    {
        var row = await context.Set<AssistantPlatformSettings>().AsNoTracking().Where(value => value.Id == AssistantPlatformSettings.SingletonId)
            .Select(value => new PlatformAssistantSettingsResponse(value.DefaultModelId, value.AllowedModelIds, Enum.Parse<AssistantEffort>(value.Effort), value.DailyMessageLimit,
                value.MonthlyOrganizationBudget, value.PersonalEnabled, value.RetentionDays, value.EncryptedApiKey != null, value.MaskedKeySuffix, value.Version)).SingleOrDefaultAsync(ct);
        return row ?? new PlatformAssistantSettingsResponse(Guid.Empty, [], AssistantEffort.Medium, AssistantFeature.DefaultDailyMessageLimit,
            new Money(0m, CurrencyCode.Create(AssistantContractPolicy.BillingCurrency).Value), false, AssistantFeature.DefaultRetentionDays, false, null, 0);
    }
}
