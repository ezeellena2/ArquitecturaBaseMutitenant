using System.Text.Json;
using System.Text.Json.Serialization;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Api.Json;

/// <summary>Usa el value object Email al deserializar para entregar al dominio una dirección ya normalizada.</summary>
internal sealed class EmailJsonConverter : JsonConverter<Email>
{
    public override Email Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var result = Email.Create(reader.GetString());
        return result.IsSuccess ? result.Value : throw new JsonException("The email is invalid.");
    }

    public override void Write(Utf8JsonWriter writer, Email value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
