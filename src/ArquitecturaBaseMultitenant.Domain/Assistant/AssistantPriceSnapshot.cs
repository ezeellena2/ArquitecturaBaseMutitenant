using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

/// <summary>Tarifas por millón históricas. Caché y tier largo provienen de la referencia técnica absoluta.</summary>
public sealed class AssistantPriceSnapshot
{
    private AssistantPriceSnapshot() { }
    public Guid ModelId { get; private set; }
    public string ProviderModelId { get; private set; } = string.Empty;
    public string Revision { get; private set; } = string.Empty;
    public string BillingCurrency { get; private set; } = string.Empty;
    public Money Input { get; private set; } = null!;
    public Money Output { get; private set; } = null!;
    public Money CacheWrite5Minutes { get; private set; } = null!;
    public Money CacheWrite1Hour { get; private set; } = null!;
    public Money CacheRead { get; private set; } = null!;
    public int? LongPromptThreshold { get; private set; }
    public Money? LongInput { get; private set; }
    public Money? LongOutput { get; private set; }
    public Money? LongCacheWrite5Minutes { get; private set; }
    public Money? LongCacheWrite1Hour { get; private set; }
    public Money? LongCacheRead { get; private set; }
    public static AssistantPriceSnapshot Create(Guid modelId, string providerModelId, string revision,
        Money input, Money output, Money cacheWrite5Minutes, Money cacheWrite1Hour, Money cacheRead,
        int? longThreshold, Money? longInput, Money? longOutput, Money? longCacheWrite5Minutes, Money? longCacheWrite1Hour, Money? longCacheRead)
    {
        if (modelId == Guid.Empty) throw new ArgumentException("A model is required.", nameof(modelId));
        ArgumentException.ThrowIfNullOrWhiteSpace(providerModelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);
        var prices = new[] { input, output, cacheWrite5Minutes, cacheWrite1Hour, cacheRead, longInput, longOutput,
            longCacheWrite5Minutes, longCacheWrite1Hour, longCacheRead };
        if (prices.Take(5).Any(price => price is null) || prices.OfType<Money>().Any(price => price.Amount < 0
            || price.Amount >= 1_000_000_000_000m || price.Round(6).Amount != price.Amount || !price.Currency.Equals(input.Currency)))
            throw new ArgumentException("Snapshot prices must have a single currency and six decimal places.");
        if (longThreshold is <= 0 || (longThreshold is null) != prices.Skip(5).All(price => price is null)
            || longThreshold is not null && prices.Skip(5).Any(price => price is null)) throw new ArgumentException("A long tier must be complete.");
        return new() { ModelId = modelId, ProviderModelId = providerModelId, Revision = revision, BillingCurrency = input.Currency.Value,
            Input = input, Output = output, CacheWrite5Minutes = cacheWrite5Minutes, CacheWrite1Hour = cacheWrite1Hour,
            CacheRead = cacheRead, LongPromptThreshold = longThreshold, LongInput = longInput, LongOutput = longOutput,
            LongCacheWrite5Minutes = longCacheWrite5Minutes, LongCacheWrite1Hour = longCacheWrite1Hour, LongCacheRead = longCacheRead };
    }
}
