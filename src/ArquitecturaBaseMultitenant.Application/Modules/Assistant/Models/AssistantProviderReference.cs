using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

/// <summary>Referencia efectiva del catálogo persistido o preset publicado para el bootstrap inicial.</summary>
public sealed record AssistantProviderReference(string Revision, string ProviderModelId, string NameEs, string NameEn,
    Money InputPerMillion, Money OutputPerMillion, Money CacheWrite5MinutesPerMillion,
    Money CacheWrite1HourPerMillion, Money CacheReadPerMillion, int? LongPromptThreshold,
    Money? LongInputPerMillion, Money? LongOutputPerMillion, Money? LongCacheWrite5MinutesPerMillion,
    Money? LongCacheWrite1HourPerMillion, Money? LongCacheReadPerMillion,
    bool Text, bool Images, bool Pdf, bool Tools, IReadOnlyList<AssistantEffort> Efforts,
    bool AdaptiveThinking = false, int MaxInputTokens = 0, int MaxOutputTokens = 0,
    bool InitialDefault = false, bool InitialAllowed = false);
