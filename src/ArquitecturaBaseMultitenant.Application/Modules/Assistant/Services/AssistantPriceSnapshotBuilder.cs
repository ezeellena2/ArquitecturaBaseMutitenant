using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal static class AssistantPriceSnapshotBuilder
{
    public static AssistantPriceSnapshot Build(AssistantModelPriceResponse model, AssistantProviderReference reference)
    {
        if (model.ProviderModelId != reference.ProviderModelId || !AssistantContractPolicy.IsValidTariff(model.InputPricePerMillion)
            || !AssistantContractPolicy.IsValidTariff(model.OutputPricePerMillion)) throw new ArgumentException("The reference must match the server model.");
        return AssistantPriceSnapshot.Create(model.Id, model.ProviderModelId, reference.Revision,
            model.InputPricePerMillion, model.OutputPricePerMillion, reference.CacheWrite5MinutesPerMillion,
            reference.CacheWrite1HourPerMillion, reference.CacheReadPerMillion, reference.LongPromptThreshold,
            reference.LongInputPerMillion, reference.LongOutputPerMillion, reference.LongCacheWrite5MinutesPerMillion,
            reference.LongCacheWrite1HourPerMillion, reference.LongCacheReadPerMillion);
    }
    public static AssistantPriceSnapshot Copy(AssistantPriceSnapshot price) => AssistantPriceSnapshot.Create(price.ModelId,
        price.ProviderModelId, price.Revision, Clone(price.Input)!, Clone(price.Output)!, Clone(price.CacheWrite5Minutes)!, Clone(price.CacheWrite1Hour)!,
        Clone(price.CacheRead)!, price.LongPromptThreshold, Clone(price.LongInput), Clone(price.LongOutput), Clone(price.LongCacheWrite5Minutes),
        Clone(price.LongCacheWrite1Hour), Clone(price.LongCacheRead));
    private static Money? Clone(Money? money) => money is null ? null : new Money(money.Amount, money.Currency);
}
