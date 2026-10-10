using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;

public sealed record AssistantModelTechnicalHttpRequest(bool? SupportsText, bool? SupportsTools, bool? SupportsImages,
    bool? SupportsPdf, bool? SupportsAdaptiveThinking, IReadOnlyList<AssistantEffort>? Efforts,
    int? MaxInputTokens, int? MaxOutputTokens, Money? CacheWrite5MinutesPricePerMillion,
    Money? CacheWrite1HourPricePerMillion, Money? CacheReadPricePerMillion, AssistantModelLongContextHttpRequest? LongContext)
{
    public AssistantModelTechnicalRequest ToModel() => new(SupportsText, SupportsTools, SupportsImages, SupportsPdf,
        SupportsAdaptiveThinking, Efforts, MaxInputTokens, MaxOutputTokens, CacheWrite5MinutesPricePerMillion,
        CacheWrite1HourPricePerMillion, CacheReadPricePerMillion, LongContext?.ToModel());
}

public sealed record AssistantModelLongContextHttpRequest(int? InputTokensThreshold, Money? InputPricePerMillion,
    Money? OutputPricePerMillion, Money? CacheWrite5MinutesPricePerMillion, Money? CacheWrite1HourPricePerMillion,
    Money? CacheReadPricePerMillion)
{
    public AssistantModelLongContextRequest ToModel() => new(InputTokensThreshold, InputPricePerMillion,
        OutputPricePerMillion, CacheWrite5MinutesPricePerMillion, CacheWrite1HourPricePerMillion, CacheReadPricePerMillion);
}
