using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantModelTechnicalRequest(bool? SupportsText, bool? SupportsTools, bool? SupportsImages,
    bool? SupportsPdf, bool? SupportsAdaptiveThinking, IReadOnlyList<AssistantEffort>? Efforts,
    int? MaxInputTokens, int? MaxOutputTokens, Money? CacheWrite5MinutesPricePerMillion,
    Money? CacheWrite1HourPricePerMillion, Money? CacheReadPricePerMillion, AssistantModelLongContextRequest? LongContext);

public sealed record AssistantModelLongContextRequest(int? InputTokensThreshold, Money? InputPricePerMillion,
    Money? OutputPricePerMillion, Money? CacheWrite5MinutesPricePerMillion, Money? CacheWrite1HourPricePerMillion,
    Money? CacheReadPricePerMillion);
