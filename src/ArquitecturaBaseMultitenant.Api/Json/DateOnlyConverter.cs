using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Api.Json;

/// <summary>Exige y emite fechas civiles sin zona en el formato fijo yyyy-MM-dd del contrato.</summary>
public sealed class DateOnlyConverter : JsonConverter<DateOnly>
{
    private const string Format = "yyyy-MM-dd";

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        return DateOnly.TryParseExact(text, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : throw new JsonException("Civil dates must use yyyy-MM-dd.");
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
}
