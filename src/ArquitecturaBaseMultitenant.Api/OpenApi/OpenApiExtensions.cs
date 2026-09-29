using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;

namespace ArquitecturaBaseMultitenant.Api.OpenApi;

internal static class OpenApiExtensions
{
    private const string DocumentUrl = "/openapi/v1.json";

    public static IServiceCollection AddOpenApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddSchemaTransformer((schema, context, _) =>
            {
                // Per-property input converters can otherwise leave string schemas empty.
                if (context.JsonTypeInfo.Type == typeof(string))
                {
                    schema.Type = IsNullable(context.JsonPropertyInfo)
                        ? JsonSchemaType.String | JsonSchemaType.Null
                        : JsonSchemaType.String;
                }

                var valueType = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type)
                    ?? context.JsonTypeInfo.Type;
                if (valueType.IsEnum)
                {
                    schema.Type = IsNullable(context.JsonPropertyInfo)
                        || Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) is not null
                            ? JsonSchemaType.String | JsonSchemaType.Null
                            : JsonSchemaType.String;
                }
                if (valueType == typeof(DateTime) || valueType == typeof(DateTimeOffset))
                {
                    schema.Type = IsNullable(context.JsonPropertyInfo)
                        || Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) is not null
                            ? JsonSchemaType.String | JsonSchemaType.Null
                            : JsonSchemaType.String;
                    schema.Format = "date-time";
                }

                if (context.JsonTypeInfo.Kind == JsonTypeInfoKind.Object && schema.Required is not null)
                {
                    foreach (var property in context.JsonTypeInfo.Properties.Where(IsNullable))
                    {
                        schema.Required.Remove(property.Name);
                    }
                }

                if (typeof(PagedRequest).IsAssignableFrom(context.JsonTypeInfo.Type) && schema.Properties is not null)
                {
                    schema.Properties["pageSize"] = CreatePageSizeSchema();
                }

                if (context.JsonTypeInfo.Type == typeof(ProblemDetails))
                {
                    DescribeProblemExtensions(schema);
                }

                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, _, _) =>
            {
                if (operation.Parameters is not null)
                {
                    foreach (var parameter in operation.Parameters.OfType<OpenApiParameter>()
                        .Where(parameter => parameter.In == ParameterLocation.Query &&
                            string.Equals(parameter.Name, "pageSize", StringComparison.OrdinalIgnoreCase)))
                    {
                        parameter.Schema = CreatePageSizeSchema();
                    }
                }

                return Task.CompletedTask;
            });

            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Components ??= new OpenApiComponents();
                document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
                document.Components.Schemas["PagedRequest"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
                    {
                        ["page"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
                        ["pageSize"] = CreatePageSizeSchema(),
                        ["sort"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                        ["search"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                    },
                };

                return Task.CompletedTask;
            });
        });
        services.Configure<MvcOptions>(options => options.Conventions.Add(new ProblemResponsesConvention()));
        return services;
    }

    private static OpenApiSchema CreatePageSizeSchema() => new()
    {
        Type = JsonSchemaType.Integer,
        Format = "int32",
        Enum = PagedRequest.AllowedPageSizes.Select(size => (JsonNode)JsonValue.Create(size)!).ToList(),
    };

    private static bool IsNullable(JsonPropertyInfo? property) =>
        property?.AttributeProvider is PropertyInfo source
        && new NullabilityInfoContext().Create(source).ReadState == NullabilityState.Nullable;

    private static void DescribeProblemExtensions(OpenApiSchema schema)
    {
        schema.Properties ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        schema.Properties["code"] = new OpenApiSchema { Type = JsonSchemaType.String };
        schema.Properties["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String };
        schema.Properties["errors"] = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            AdditionalProperties = new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Items = new OpenApiSchema { Type = JsonSchemaType.String },
            },
        };
        schema.Properties["retryAfter"] = new OpenApiSchema
        {
            Type = JsonSchemaType.Integer | JsonSchemaType.Null,
            Format = "int32",
        };
        schema.Required ??= new HashSet<string>(StringComparer.Ordinal);
        schema.Required.Add("code");
        schema.Required.Add("traceId");
    }

    public static WebApplication MapOpenApiDocumentation(this WebApplication app)
    {
        app.MapOpenApi();
        app.UseSwaggerUI(options => options.SwaggerEndpoint(DocumentUrl, "ArquitecturaBaseMultitenant v1"));
        return app;
    }
}
