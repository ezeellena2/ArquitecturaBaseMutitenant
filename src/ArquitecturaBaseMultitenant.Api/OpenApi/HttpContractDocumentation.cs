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
        if (Find(document, path, method) is not { } operation) return;
        operation.Description = string.IsNullOrEmpty(operation.Description) ? description : operation.Description + " " + description;
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
        AddErrors(document, operation, errors);
        if (examples is not null && operation.RequestBody?.Content?.TryGetValue("application/json", out var media) == true)
            media.Examples = examples.ToDictionary(pair => pair.Key, pair => (IOpenApiExample)new OpenApiExample { Value = pair.Value });
    }

    /// <summary>
    /// Suma al catálogo y a las respuestas de una operación los errores que todavía no declara, sin tocar su
    /// descripción: así una regla transversal (cuotas, prueba reciente) se aplica a rutas de cualquier contribuidor.
    /// </summary>
    public static void AddErrors(OpenApiDocument document, string path, HttpMethod method, IReadOnlyList<HttpContractError> errors)
    {
        if (Find(document, path, method) is not { } operation) return;
        var declared = Catalog(operation)
            .Select(value => (value?["status"]?.GetValue<int>() ?? 0, value?["code"]?.GetValue<string>() ?? string.Empty))
            .ToHashSet();
        AddErrors(document, operation, errors.Where(error => !declared.Contains((error.Status, error.Code))).ToArray());
    }

    public static OpenApiOperation? Find(OpenApiDocument document, string path, HttpMethod method) =>
        document.Paths.TryGetValue(path, out var item) && item.Operations is not null
            && item.Operations.TryGetValue(method, out var operation) ? operation : null;

    private static JsonArray Catalog(OpenApiOperation operation) =>
        operation.Extensions?.TryGetValue("x-error-codes", out var previous) == true && previous is JsonNodeExtension { Node: JsonArray existing }
            ? existing : [];

    private static void AddErrors(OpenApiDocument document, OpenApiOperation operation, IReadOnlyList<HttpContractError> errors)
    {
        operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        var catalog = (JsonArray)Catalog(operation).DeepClone();
        foreach (var error in errors) catalog.Add(new JsonObject
        {
            ["status"] = error.Status, ["code"] = error.Code, ["field"] = error.Field,
            ["es"] = error.Spanish, ["en"] = error.English,
        });
        operation.Extensions["x-error-codes"] = new JsonNodeExtension(catalog);
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
