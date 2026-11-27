using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Api.Json;

/// <summary>Aplica a MVC y respuestas HTTP los formatos del contrato y normaliza strings de entrada salvo los marcados RawText.</summary>
public static class JsonConfiguration
{
    public static void ConfigureJson(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Converters.Add(new UtcDateTimeConverter());
        options.Converters.Add(new DateOnlyConverter());
        options.Converters.Add(new TimeOnlyConverter());
        options.Converters.Add(new MoneyJsonConverter());
        options.Converters.Add(new EmailJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter<Access>(JsonNamingPolicy.CamelCase,
            allowIntegerValues: false));
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        options.NumberHandling = JsonNumberHandling.Strict;

        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(typeInfo =>
        {
            // Un pedido y las partes que lleva adentro (*HttpInput, como una opción de un catálogo) limpian igual sus textos.
            if (!typeInfo.Type.Name.EndsWith("HttpRequest", StringComparison.Ordinal)
                && !typeInfo.Type.Name.EndsWith("HttpInput", StringComparison.Ordinal))
            {
                return;
            }

            foreach (var property in typeInfo.Properties)
            {
                if (property.CustomConverter is not null
                    || property.AttributeProvider?.IsDefined(typeof(RawTextAttribute), inherit: true) == true)
                {
                    continue;
                }

                // Una lista de textos (etiquetas, valores de una condición) limpia cada uno como un texto suelto.
                property.CustomConverter = property.PropertyType switch
                {
                    var type when type == typeof(string) => new NormalizedStringJsonConverter(),
                    var type when type == typeof(IReadOnlyList<string>) => new NormalizedStringListJsonConverter<IReadOnlyList<string>>(),
                    var type when type == typeof(IReadOnlyCollection<string>) =>
                        new NormalizedStringListJsonConverter<IReadOnlyCollection<string>>(),
                    _ => null,
                };
            }
        });
        options.TypeInfoResolverChain.Insert(0, resolver);
    }
}
