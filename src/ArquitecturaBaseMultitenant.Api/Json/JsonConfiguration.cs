using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Api.Json;

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
            if (!typeInfo.Type.Name.EndsWith("HttpRequest", StringComparison.Ordinal))
            {
                return;
            }

            foreach (var property in typeInfo.Properties.Where(property => property.PropertyType == typeof(string)))
            {
                if (property.CustomConverter is null
                    && property.AttributeProvider?.IsDefined(typeof(RawTextAttribute), inherit: true) != true)
                {
                    property.CustomConverter = new NormalizedStringJsonConverter();
                }
            }
        });
        options.TypeInfoResolverChain.Insert(0, resolver);
    }
}
