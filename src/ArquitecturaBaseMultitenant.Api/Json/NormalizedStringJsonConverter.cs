using System.Text.Json;
using System.Text.Json.Serialization;
using ArquitecturaBaseMultitenant.Application.Common.Text;

namespace ArquitecturaBaseMultitenant.Api.Json;

/// <summary>Limpia todo string de entrada antes de entregarlo al contrato HTTP.</summary>
internal sealed class NormalizedStringJsonConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? null : TextNormalizer.Clean(reader.GetString());

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
