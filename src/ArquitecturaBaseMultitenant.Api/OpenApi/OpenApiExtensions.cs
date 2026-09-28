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
            // The input normalizer is a JsonConverter<string>; OpenAPI otherwise emits
            // an empty schema for every string, producing `unknown` in generated clients.
            if (context.JsonTypeInfo.Type == typeof(string))
            {
                schema.Type = JsonSchemaType.String;
            }

            return Task.CompletedTask;
        }));
        services.Configure<MvcOptions>(options => options.Conventions.Add(new ProblemResponsesConvention()));
        return services;
    }

    public static WebApplication MapOpenApiDocumentation(this WebApplication app)
    {
        app.MapOpenApi();
        app.UseSwaggerUI(options => options.SwaggerEndpoint(DocumentUrl, "ArquitecturaBaseMultitenant v1"));
        return app;
    }
}
