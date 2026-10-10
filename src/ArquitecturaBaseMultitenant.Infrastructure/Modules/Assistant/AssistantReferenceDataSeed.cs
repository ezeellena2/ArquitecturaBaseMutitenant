using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

internal sealed class AssistantReferenceDataSeed(ApplicationDbContext context, IAssistantProviderCatalog references,
    ISecurityEventRepository events, TimeProvider clock) : IModuleDataSeedContributor
{
    public async Task StageAsync(CancellationToken ct)
    {
        context.RequireTransaction();
        var models = await context.Set<AssistantModelPrice>().ToListAsync(ct);
        var hasSettings = await context.Set<AssistantPlatformSettings>().AnyAsync(ct);
        if (hasSettings && references.InitialModels.All(reference => models.Any(model => model.Provider == nameof(AssistantProvider.Anthropic)
            && model.ProviderModelId == reference.ProviderModelId))) return;
        var currency = CurrencyCode.Create(AssistantContractPolicy.BillingCurrency).Value;
        if (!await context.Set<ArquitecturaBaseMultitenant.Domain.ReferenceData.Currency>().AnyAsync(value => value.Code == currency.Value && value.IsEnabled, ct)
            && !context.ChangeTracker.Entries<ArquitecturaBaseMultitenant.Domain.ReferenceData.Currency>().Any(value => value.Entity.Code == currency.Value && value.Entity.IsEnabled))
            throw new InvalidOperationException("The billing currency reference must be enabled before the assistant seed.");
        var changed = false;
        foreach (var reference in references.InitialModels)
        {
            if (models.Any(value => value.Provider == nameof(AssistantProvider.Anthropic) && value.ProviderModelId == reference.ProviderModelId)) continue;
            var model = AssistantModelPrice.Create(nameof(AssistantProvider.Anthropic), reference.ProviderModelId, reference.NameEs,
                reference.InputPerMillion, reference.OutputPerMillion);
            model.Update(model.Provider, model.ProviderModelId, model.DisplayName, model.InputPrice, model.OutputPrice, true);
            context.Add(model);
            models.Add(model);
            changed = true;
        }
        if (!hasSettings)
        {
            var sonnet = models.Single(value => value.ProviderModelId == "claude-sonnet-5-5").Id;
            var haiku = models.Single(value => value.ProviderModelId == "claude-haiku-5-5").Id;
            var settings = AssistantPlatformSettings.Create(false, new Money(100m, currency));
            settings.Update(sonnet, [sonnet, haiku], nameof(AssistantEffort.Low), AssistantFeature.DefaultDailyMessageLimit,
                new Money(100m, currency), false, AssistantFeature.DefaultRetentionDays);
            context.Add(settings);
            changed = true;
        }
        if (changed) events.Add(SecurityEvent.ForPlatformSettings(AuditActorKind.System, Guid.Empty,
            "Initial assistant reference seed", clock.GetUtcNow().UtcDateTime));
    }
}
