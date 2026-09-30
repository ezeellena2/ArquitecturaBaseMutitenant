using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Api.Json;

/// <summary>Exige y emite horas civiles sin fecha ni zona en el formato fijo HH:mm:ss del contrato.</summary>
public sealed class TimeOnlyConverter : JsonConverter<TimeOnly>
{
    private const string Format = "HH:mm:ss";

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        return TimeOnly.TryParseExact(text, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : throw new JsonException("Civil times must use HH:mm:ss.");
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
}
