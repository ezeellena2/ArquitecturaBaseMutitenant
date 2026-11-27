using System.Text.Json;
using System.Text.Json.Serialization;
using ArquitecturaBaseMultitenant.Application.Common.Text;

namespace ArquitecturaBaseMultitenant.Api.Json;

/// <summary>
/// Limpia cada texto de una lista de entrada (las etiquetas o los valores de una condición) como
/// <see cref="NormalizedStringJsonConverter"/>. Un elemento que queda vacío pasa a <c>""</c>, no a <c>null</c>: la regla de la
/// lista lo rechaza como vacío, sin un elemento nulo que nadie espera.
/// </summary>
internal sealed class NormalizedStringListJsonConverter<TList> : JsonConverter<TList>
    where TList : class, IEnumerable<string>
{
    public override TList? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("A list of texts was expected.");
        var values = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            values.Add(reader.TokenType == JsonTokenType.Null ? null! : TextNormalizer.Clean(reader.GetString()) ?? string.Empty);
        return (TList)(object)values;
    }

    public override void Write(Utf8JsonWriter writer, TList value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartArray();
        foreach (var item in value) writer.WriteStringValue(item);
        writer.WriteEndArray();
    }
}
