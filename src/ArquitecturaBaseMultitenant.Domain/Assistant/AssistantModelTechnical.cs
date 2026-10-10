using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

/// <summary>Metadata editable de la fila del catálogo. Null en la entidad significa referencia heredada incompleta.</summary>
public sealed record AssistantModelTechnical
{
    private AssistantModelTechnical() { }
    public bool SupportsText { get; private set; }
    public bool SupportsTools { get; private set; }
    public bool SupportsImages { get; private set; }
    public bool SupportsPdf { get; private set; }
    public bool SupportsAdaptiveThinking { get; private set; }
    public bool LowEffort { get; private set; }
    public bool MediumEffort { get; private set; }
    public bool HighEffort { get; private set; }
    public int MaxInputTokens { get; private set; }
    public int MaxOutputTokens { get; private set; }
    public Money CacheWrite5Minutes { get; private set; } = null!;
    public Money CacheWrite1Hour { get; private set; } = null!;
    public Money CacheRead { get; private set; } = null!;
    public int? LongInputTokensThreshold { get; private set; }
    public Money? LongInput { get; private set; }
    public Money? LongOutput { get; private set; }
    public Money? LongCacheWrite5Minutes { get; private set; }
    public Money? LongCacheWrite1Hour { get; private set; }
    public Money? LongCacheRead { get; private set; }

    public static AssistantModelTechnical Create(bool text, bool tools, bool images, bool pdf, bool adaptive,
        bool low, bool medium, bool high, int maxInput, int maxOutput, Money cache5Minutes, Money cache1Hour,
        Money cacheRead, int? threshold, Money? longInput, Money? longOutput, Money? longCache5Minutes,
        Money? longCache1Hour, Money? longCacheRead)
    {
        if (maxInput <= 0 || maxOutput <= 0 || maxOutput > maxInput || !(low || medium || high)
            || threshold is <= 0 || threshold >= maxInput)
            throw new ArgumentException("Model limits and efforts must be coherent.");
        var prices = new[] { cache5Minutes, cache1Hour, cacheRead, longInput, longOutput, longCache5Minutes, longCache1Hour, longCacheRead };
        if (prices.Take(3).Any(value => value is null) || prices.OfType<Money>().Any(value => value.Amount <= 0
            || value.Amount >= 1_000_000_000_000m || value.Round(6).Amount != value.Amount || value.Currency != cacheRead.Currency)
            || (threshold is null ? prices.Skip(3).Any(value => value is not null) : prices.Skip(3).Any(value => value is null)))
            throw new ArgumentException("Technical tariffs must be positive, complete and have six decimal places in one currency.");
        return new() { SupportsText = text, SupportsTools = tools, SupportsImages = images, SupportsPdf = pdf,
            SupportsAdaptiveThinking = adaptive, LowEffort = low, MediumEffort = medium, HighEffort = high,
            MaxInputTokens = maxInput, MaxOutputTokens = maxOutput, CacheWrite5Minutes = Copy(cache5Minutes)!,
            CacheWrite1Hour = Copy(cache1Hour)!, CacheRead = Copy(cacheRead)!, LongInputTokensThreshold = threshold,
            LongInput = Copy(longInput), LongOutput = Copy(longOutput), LongCacheWrite5Minutes = Copy(longCache5Minutes),
            LongCacheWrite1Hour = Copy(longCache1Hour), LongCacheRead = Copy(longCacheRead) };
    }
    internal void UpdateFrom(AssistantModelTechnical value)
    {
        SupportsText = value.SupportsText;
        SupportsTools = value.SupportsTools;
        SupportsImages = value.SupportsImages;
        SupportsPdf = value.SupportsPdf;
        SupportsAdaptiveThinking = value.SupportsAdaptiveThinking;
        LowEffort = value.LowEffort;
        MediumEffort = value.MediumEffort;
        HighEffort = value.HighEffort;
        MaxInputTokens = value.MaxInputTokens;
        MaxOutputTokens = value.MaxOutputTokens;
        LongInputTokensThreshold = value.LongInputTokensThreshold;
        CacheWrite5Minutes = value.CacheWrite5Minutes;
        CacheWrite1Hour = value.CacheWrite1Hour;
        CacheRead = value.CacheRead;
        LongInput = value.LongInput;
        LongOutput = value.LongOutput;
        LongCacheWrite5Minutes = value.LongCacheWrite5Minutes;
        LongCacheWrite1Hour = value.LongCacheWrite1Hour;
        LongCacheRead = value.LongCacheRead;
    }
    private static Money? Copy(Money? value) => value is null ? null : new(value.Amount, value.Currency);
}
