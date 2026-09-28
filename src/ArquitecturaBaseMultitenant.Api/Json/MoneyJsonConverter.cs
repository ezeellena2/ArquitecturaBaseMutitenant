using System.Text.Json;
using System.Text.Json.Serialization;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Api.Json;

/// <summary>Serializa Money como importe decimal y código ISO, sin consultar el catálogo.</summary>
internal sealed class MoneyJsonConverter : JsonConverter<Money>
{
    public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var value = document.RootElement;
        if (value.ValueKind != JsonValueKind.Object
            || !value.TryGetProperty("amount", out var amountElement)
            || amountElement.ValueKind != JsonValueKind.Number
            || !amountElement.TryGetDecimal(out var amount)
            || !value.TryGetProperty("currency", out var currencyElement)
            || currencyElement.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("Money requires a decimal amount and currency code.");
        }

        var currency = CurrencyCode.Create(currencyElement.GetString());
        if (currency.IsFailure)
        {
            throw new JsonException("Money currency code has invalid syntax.");
        }

        return new Money(amount, currency.Value);
    }

    public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("amount", value.Amount);
        writer.WriteString("currency", value.Currency.Value);
        writer.WriteEndObject();
    }
}
