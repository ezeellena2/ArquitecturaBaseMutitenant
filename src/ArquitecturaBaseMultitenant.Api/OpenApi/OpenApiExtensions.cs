using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;

namespace ArquitecturaBaseMultitenant.Api.OpenApi;

internal static class OpenApiExtensions
{
    private const string DocumentUrl = "/openapi/v1.json";

    public static IServiceCollection AddOpenApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi(options => options.AddSchemaTransformer((schema, context, _) =>
        {
            // Per-property input converters can otherwise leave string schemas empty.
            if (context.JsonTypeInfo.Type == typeof(string))
            {
                schema.Type = IsNullable(context.JsonPropertyInfo)
                    ? JsonSchemaType.String | JsonSchemaType.Null
                    : JsonSchemaType.String;
            }

            if (context.JsonTypeInfo.Kind == JsonTypeInfoKind.Object && schema.Required is not null)
            {
                foreach (var property in context.JsonTypeInfo.Properties.Where(IsNullable))
                {
                    schema.Required.Remove(property.Name);
                }
            }

            return Task.CompletedTask;
        }));
        services.Configure<MvcOptions>(options => options.Conventions.Add(new ProblemResponsesConvention()));
        return services;
    }

    private static bool IsNullable(JsonPropertyInfo? property) =>
        property?.AttributeProvider is PropertyInfo source
        && new NullabilityInfoContext().Create(source).ReadState == NullabilityState.Nullable;

    public static WebApplication MapOpenApiDocumentation(this WebApplication app)
    {
        app.MapOpenApi();
        app.UseSwaggerUI(options => options.SwaggerEndpoint(DocumentUrl, "ArquitecturaBaseMultitenant v1"));
        return app;
    }
}
