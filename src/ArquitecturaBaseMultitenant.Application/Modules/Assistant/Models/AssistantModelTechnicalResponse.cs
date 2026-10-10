using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantModelTechnicalResponse(bool SupportsText, bool SupportsTools, bool SupportsImages,
    bool SupportsPdf, bool SupportsAdaptiveThinking, IReadOnlyList<AssistantEffort> Efforts,
    int MaxInputTokens, int MaxOutputTokens, Money CacheWrite5MinutesPricePerMillion,
    Money CacheWrite1HourPricePerMillion, Money CacheReadPricePerMillion, AssistantModelLongContextResponse? LongContext);

public sealed record AssistantModelLongContextResponse(int InputTokensThreshold, Money InputPricePerMillion,
    Money OutputPricePerMillion, Money CacheWrite5MinutesPricePerMillion, Money CacheWrite1HourPricePerMillion,
    Money CacheReadPricePerMillion);
