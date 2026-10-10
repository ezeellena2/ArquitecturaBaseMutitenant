using System.Globalization;
using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Application.Resources;
using Microsoft.OpenApi;

namespace ArquitecturaBaseMultitenant.Api.OpenApi;

public sealed record HttpContractError(int Status, string Code, string? Field, string Spanish, string English);

/// <summary>Forma común de ejemplos, catálogo de errores y headers; cada contribuidor declara su conducta.</summary>
public static class HttpContractDocumentation
{
    public static void Describe(OpenApiDocument document, string path, HttpMethod method, string description,
        IReadOnlyList<HttpContractError> errors, bool idempotent = false, bool authenticated = false,
        IReadOnlyDictionary<string, JsonNode>? examples = null)
    {
        if (!document.Paths.TryGetValue(path, out var item) || item.Operations is null
            || !item.Operations.TryGetValue(method, out var operation)) return;
        operation.Description = string.IsNullOrEmpty(operation.Description) ? description : operation.Description + " " + description;
        operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        var catalog = operation.Extensions.TryGetValue("x-error-codes", out var previous) && previous is JsonNodeExtension { Node: JsonArray existing }
            ? (JsonArray)existing.DeepClone() : new JsonArray();
        foreach (var error in errors) catalog.Add(new JsonObject
        {
            ["status"] = error.Status, ["code"] = error.Code, ["field"] = error.Field,
            ["es"] = error.Spanish, ["en"] = error.English,
        });
        operation.Extensions["x-error-codes"] = new JsonNodeExtension(catalog);
        if (authenticated)
            operation.Security = [new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
            }];
        else if (operation.Security is null) operation.Security = [];
        if (idempotent)
        {
            operation.Parameters ??= [];
            if (!operation.Parameters.Any(value => value.Name == "Idempotency-Key"))
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = "Idempotency-Key", In = ParameterLocation.Header, Required = true,
                    Description = "UUID por operación. Reusar la misma key y cuerpo para reintentar; otra key representa otra operación.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
                });
        }
        operation.Responses ??= new OpenApiResponses();
        foreach (var group in errors.GroupBy(error => error.Status))
        {
            var response = new OpenApiResponse
            {
                Description = string.Join("; ", group.Select(error => error.Code)),
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/problem+json"] = new()
                    {
                        Schema = new OpenApiSchemaReference("ProblemDetails", document),
                        Examples = new Dictionary<string, IOpenApiExample>(),
                    },
                },
            };
            if (operation.Responses.TryGetValue(StatusName(group.Key), out var previousResponse)
                && previousResponse.Content?.TryGetValue("application/problem+json", out var previousMedia) == true)
            {
                response.Description = previousResponse.Description + "; " + response.Description;
                foreach (var example in previousMedia.Examples ?? new Dictionary<string, IOpenApiExample>())
                    response.Content["application/problem+json"].Examples![example.Key] = example.Value;
            }
            foreach (var error in group)
                foreach (var culture in new[] { "es", "en" })
                {
                    var detail = culture == "es" ? error.Spanish : error.English;
                    var example = new JsonObject
                    {
                        ["type"] = "about:blank", ["title"] = culture == "es" ? "Solicitud rechazada" : "Request rejected",
                        ["status"] = error.Status, ["detail"] = detail, ["code"] = error.Code, ["traceId"] = "example-trace-id",
                    };
                    if (error.Field is not null) example["errors"] = new JsonObject { [error.Field] = new JsonArray(detail) };
                    if (error.Status == 429 && error.Code != "Identity.Account.LockedOut") example["retryAfter"] = 60;
                    response.Content["application/problem+json"].Examples![error.Code + "." + culture] = new OpenApiExample { Value = example };
                }
            if (group.Key == 429) response.Headers = new Dictionary<string, IOpenApiHeader>
            {
                ["Retry-After"] = new OpenApiHeader { Description = "Opcional: segundos iguales a retryAfter cuando la respuesta incluye esa metadata entera. Identity.Account.LockedOut no incluye plazo ni este encabezado.", Schema = new OpenApiSchema { Type = JsonSchemaType.Integer } },
            };
            operation.Responses[StatusName(group.Key)] = response;
        }
        if (examples is not null && operation.RequestBody?.Content?.TryGetValue("application/json", out var media) == true)
            media.Examples = examples.ToDictionary(pair => pair.Key, pair => (IOpenApiExample)new OpenApiExample { Value = pair.Value });
    }

    private static string StatusName(int status) => status switch
    {
        400 => "400", 401 => "401", 403 => "403", 404 => "404", 409 => "409", 413 => "413", 422 => "422",
        429 => "429", 500 => "500", 501 => "501", 503 => "503",
        _ => throw new ArgumentOutOfRangeException(nameof(status), "An error response status must be explicitly documented."),
    };

    public static HttpContractError CoreError(int status, string code, string? field = null)
    {
        var resources = new System.Resources.ResourceManager("ArquitecturaBaseMultitenant.Application.Resources.Errors", typeof(ErrorTexts).Assembly);
        return new HttpContractError(status, code, field,
            resources.GetString(code, CultureInfo.GetCultureInfo("es")) ?? code,
            resources.GetString(code, CultureInfo.GetCultureInfo("en")) ?? code);
    }
}
