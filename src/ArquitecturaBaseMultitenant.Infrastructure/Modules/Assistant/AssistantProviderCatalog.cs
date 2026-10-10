using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Configuration;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

internal sealed class AssistantProviderCatalog : IAssistantProviderCatalog
{
    public AssistantProviderCatalog(IOptions<AssistantOptions> options)
    {
        using var stream = GetType().Assembly.GetManifestResourceStream("AssistantReferenceData.anthropic-models-2026-10-10.json")
            ?? throw new InvalidOperationException("The provider reference catalog is missing.");
        using var document = JsonDocument.Parse(stream);
        var rows = document.RootElement.EnumerateArray().Select(row => Read(row, AssistantContractPolicy.BillingCurrency)).ToDictionary(value => value.ProviderModelId, StringComparer.Ordinal);
        InitialModels = Array.AsReadOnly(rows.Values.ToArray());
        if (options.Value.ProviderReferenceFile is { Length: > 0 } configuredFile)
        {
            if (!Path.IsPathFullyQualified(configuredFile)) throw new InvalidOperationException("Assistant:ProviderReferenceFile must be an absolute path.");
            using var external = JsonDocument.Parse(File.ReadAllBytes(configuredFile));
            var additions = external.RootElement.EnumerateArray().Select(row => Read(row, null)).ToArray();
            if (additions.Select(value => value.ProviderModelId).Distinct(StringComparer.Ordinal).Count() != additions.Length)
                throw new InvalidOperationException("The provider reference file contains duplicate model identifiers.");
            foreach (var reference in additions) rows[reference.ProviderModelId] = reference;
        }
        All = Array.AsReadOnly(rows.Values.OrderBy(value => value.ProviderModelId, StringComparer.Ordinal).ToArray());
    }
    public IReadOnlyList<AssistantProviderReference> All { get; }
    public IReadOnlyList<AssistantProviderReference> InitialModels { get; }
    public AssistantProviderReference? Find(string providerModelId) => All.SingleOrDefault(row => row.ProviderModelId == providerModelId);
    private static AssistantProviderReference Read(JsonElement row, string? embeddedCurrency)
    {
        try { return ReadValidated(row, embeddedCurrency); }
        catch (Exception error) when (error is KeyNotFoundException or FormatException or InvalidOperationException or ArgumentException or OverflowException)
        { throw new InvalidOperationException("Assistant provider reference metadata is invalid."); }
    }
    private static AssistantProviderReference ReadValidated(JsonElement row, string? embeddedCurrency)
    {
        var parsedCurrency = CurrencyCode.Create(embeddedCurrency ?? row.GetProperty("currency").GetString());
        if (parsedCurrency.IsFailure || parsedCurrency.Value.Value != AssistantContractPolicy.BillingCurrency)
            throw new InvalidOperationException("The provider billing currency is invalid.");
        var currency = parsedCurrency.Value;
        Money Amount(decimal amount) => AssistantContractPolicy.IsValidTariff(new(amount, currency)) ? new(amount, currency) : throw new InvalidOperationException("A provider price is invalid.");
        Money Price(string name) => Amount(row.GetProperty(name).GetDecimal());
        Money? Optional(string name) => row.TryGetProperty(name, out var value) ? Amount(value.GetDecimal()) : null;
        string Required(string name) => row.GetProperty(name).GetString() is { } value && !string.IsNullOrWhiteSpace(value)
            ? value : throw new InvalidOperationException("A provider reference identifier or name is missing.");
        var efforts = row.GetProperty("efforts").EnumerateArray().Select(value =>
            Enum.TryParse<AssistantEffort>(value.GetString(), out var effort) && Enum.IsDefined(effort)
                && value.GetString() == effort.ToString() ? effort : throw new InvalidOperationException("A provider effort is invalid.")).ToArray();
        if (efforts.Length == 0 || efforts.Distinct().Count() != efforts.Length)
            throw new InvalidOperationException("Provider efforts must be nonempty and unique.");
        var reference = new AssistantProviderReference(Required("revision"), Required("providerModelId"),
            Required("nameEs"), Required("nameEn"),
            Price("inputPerMillion"), Price("outputPerMillion"), Price("cacheWrite5MinutesPerMillion"), Price("cacheWrite1HourPerMillion"), Price("cacheReadPerMillion"),
            row.TryGetProperty("longPromptThreshold", out var threshold) ? threshold.GetInt32() : null,
            Optional("longInputPerMillion"), Optional("longOutputPerMillion"), Optional("longCacheWrite5MinutesPerMillion"),
            Optional("longCacheWrite1HourPerMillion"), Optional("longCacheReadPerMillion"), row.GetProperty("text").GetBoolean(),
            row.GetProperty("images").GetBoolean(), row.GetProperty("pdf").GetBoolean(), row.GetProperty("tools").GetBoolean(),
            Array.AsReadOnly(efforts));
        var longPrices = new[] { reference.LongInputPerMillion, reference.LongOutputPerMillion, reference.LongCacheWrite5MinutesPerMillion,
            reference.LongCacheWrite1HourPerMillion, reference.LongCacheReadPerMillion };
        if (reference.LongPromptThreshold is { } limit ? limit <= 0 || longPrices.Any(price => price is null) : longPrices.Any(price => price is not null))
            throw new InvalidOperationException("A provider pricing tier is incomplete.");
        return reference;
    }
}
