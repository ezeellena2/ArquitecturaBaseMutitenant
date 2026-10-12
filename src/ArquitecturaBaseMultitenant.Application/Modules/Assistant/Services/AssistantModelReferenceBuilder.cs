using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using System.Security.Cryptography;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>Construye una referencia efectiva sólo con el catálogo persistido; no consulta presets ni proveedor.</summary>
public static class AssistantModelReferenceBuilder
{
    public static AssistantModelTechnical ToDomain(AssistantModelTechnicalRequest value) =>
        AssistantModelTechnical.Create(value.SupportsText!.Value, value.SupportsTools!.Value, value.SupportsImages!.Value,
            value.SupportsPdf!.Value, value.SupportsAdaptiveThinking!.Value, value.Efforts!.Contains(AssistantEffort.Low),
            value.Efforts!.Contains(AssistantEffort.Medium), value.Efforts!.Contains(AssistantEffort.High),
            value.MaxInputTokens!.Value, value.MaxOutputTokens!.Value, value.CacheWrite5MinutesPricePerMillion!,
            value.CacheWrite1HourPricePerMillion!, value.CacheReadPricePerMillion!, value.LongContext?.InputTokensThreshold,
            value.LongContext?.InputPricePerMillion, value.LongContext?.OutputPricePerMillion,
            value.LongContext?.CacheWrite5MinutesPricePerMillion, value.LongContext?.CacheWrite1HourPricePerMillion,
            value.LongContext?.CacheReadPricePerMillion);

    public static AssistantModelTechnicalResponse? ToResponse(AssistantModelTechnical? value)
    {
        if (value is null) return null;
        var efforts = new List<AssistantEffort>();
        if (value.LowEffort) efforts.Add(AssistantEffort.Low);
        if (value.MediumEffort) efforts.Add(AssistantEffort.Medium);
        if (value.HighEffort) efforts.Add(AssistantEffort.High);
        return new(value.SupportsText, value.SupportsTools, value.SupportsImages, value.SupportsPdf,
            value.SupportsAdaptiveThinking, efforts.AsReadOnly(), value.MaxInputTokens, value.MaxOutputTokens,
            value.CacheWrite5Minutes, value.CacheWrite1Hour, value.CacheRead,
            value.LongInputTokensThreshold is { } threshold ? new(threshold, value.LongInput!, value.LongOutput!,
                value.LongCacheWrite5Minutes!, value.LongCacheWrite1Hour!, value.LongCacheRead!) : null);
    }

    public static AssistantModelPriceResponse WithRevision(AssistantModelPriceResponse model)
    {
        if (model.Technical is null) return model with { TechnicalRevision = null };
        // Explicit, stable shape: display name, enabled and xmin are not technical reference data.
        var technical = model.Technical;
        var tier = technical.LongContext;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { model.Provider, model.ProviderModelId,
            Input = Rate(model.InputPricePerMillion), Output = Rate(model.OutputPricePerMillion),
            technical.SupportsText, technical.SupportsTools, technical.SupportsImages, technical.SupportsPdf,
            technical.SupportsAdaptiveThinking, Efforts = technical.Efforts.Order().ToArray(),
            technical.MaxInputTokens, technical.MaxOutputTokens,
            Cache5 = Rate(technical.CacheWrite5MinutesPricePerMillion), CacheHour = Rate(technical.CacheWrite1HourPricePerMillion),
            CacheRead = Rate(technical.CacheReadPricePerMillion), Threshold = tier?.InputTokensThreshold,
            LongInput = Rate(tier?.InputPricePerMillion), LongOutput = Rate(tier?.OutputPricePerMillion),
            LongCache5 = Rate(tier?.CacheWrite5MinutesPricePerMillion), LongCacheHour = Rate(tier?.CacheWrite1HourPricePerMillion),
            LongCacheRead = Rate(tier?.CacheReadPricePerMillion) });
        return model with { TechnicalRevision = "db-v1-" + Convert.ToHexStringLower(SHA256.HashData(bytes)) };
    }

    // Decimal scale and effort ordering do not change an effective reference.
    private static string? Rate(Money? value) => value is null ? null
        : value.Currency.Value + ":" + InvariantWireFormatter.FormatCanonicalDecimal(value.Amount);

    public static AssistantProviderReference Build(AssistantModelPriceResponse model)
    {
        var technical = model.Technical ?? throw new ArgumentException("A complete model reference is required.", nameof(model));
        var tier = technical.LongContext;
        return new(WithRevision(model).TechnicalRevision!, model.ProviderModelId, model.Name, model.Name,
            model.InputPricePerMillion, model.OutputPricePerMillion, technical.CacheWrite5MinutesPricePerMillion,
            technical.CacheWrite1HourPricePerMillion, technical.CacheReadPricePerMillion, tier?.InputTokensThreshold,
            tier?.InputPricePerMillion, tier?.OutputPricePerMillion, tier?.CacheWrite5MinutesPricePerMillion,
            tier?.CacheWrite1HourPricePerMillion, tier?.CacheReadPricePerMillion, technical.SupportsText,
            technical.SupportsImages, technical.SupportsPdf, technical.SupportsTools, technical.Efforts,
            technical.SupportsAdaptiveThinking, technical.MaxInputTokens, technical.MaxOutputTokens);
    }
}
