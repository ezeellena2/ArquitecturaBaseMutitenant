using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

/// <summary>Referencia versionada del proveedor; no forma parte del contrato HTTP editable del catálogo.</summary>
public sealed record AssistantProviderReference(string Revision, string ProviderModelId, string NameEs, string NameEn,
    Money InputPerMillion, Money OutputPerMillion, Money CacheWrite5MinutesPerMillion,
    Money CacheWrite1HourPerMillion, Money CacheReadPerMillion, int? LongPromptThreshold,
    Money? LongInputPerMillion, Money? LongOutputPerMillion, Money? LongCacheWrite5MinutesPerMillion,
    Money? LongCacheWrite1HourPerMillion, Money? LongCacheReadPerMillion,
    bool Text, bool Images, bool Pdf, bool Tools, IReadOnlyList<AssistantEffort> Efforts);
