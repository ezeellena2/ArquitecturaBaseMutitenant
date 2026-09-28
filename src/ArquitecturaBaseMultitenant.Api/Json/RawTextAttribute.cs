using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Api.Json;

/// <summary>Preserva literalmente un token, código o texto firmado al deserializar.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class RawTextAttribute : JsonConverterAttribute
{
    public override JsonConverter CreateConverter(Type typeToConvert)
    {
        if (typeToConvert != typeof(string))
        {
            throw new InvalidOperationException("RawText can only be used on string properties.");
        }

        return new RawStringJsonConverter();
    }

    private sealed class RawStringJsonConverter : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.Null ? null : reader.GetString();

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value);
    }
}
