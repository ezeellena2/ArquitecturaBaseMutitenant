using System.Globalization;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Domain.Forms;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// La guarda de los ejemplos de una corrida real (api-http, desde la E12) para Formularios: cada respuesta JSON de éxito de cada ruta
/// y cada error de negocio que documenta openapi.json en <c>x-error-codes</c> tienen su ejemplo en
/// <c>docs/contracts/examples/forms.json</c>, que graba <c>FormsContractExamplesTests</c> y el front valida con zod estricto. Los
/// errores del núcleo que dan todas las rutas igual (401, 403, el 404 del módulo apagado, la idempotencia y el 500) no piden ejemplo.
/// </summary>
public sealed class FormsContractExamplesCoverageTests
{
    private const string Prefix = "/api/organization/forms/";
    private const string ExamplesFile = "forms.json";

    /// <summary>
    /// Lo que ninguna corrida puede dar todavía, con su motivo: un código en todas sus rutas, o una sola respuesta con la forma
    /// «MÉTODO ruta status código». La tarea que lo produce graba su ejemplo y lo saca de acá.
    /// </summary>
    private static readonly Dictionary<string, string> NotReachableYet = new(StringComparer.Ordinal)
    {
        [FormCatalogErrors.ItemInUseCode] = "nada usa una opción hasta que un campo de otro catálogo la tenga como valor (E13, B6)",
        [FormCatalogErrors.TooManyFieldsCode] = "los campos llegan con el dominio y el guardado de la E13 (B1 y B4)",
        [FormCatalogErrors.FieldKeyDuplicateCode] = "los campos llegan con el dominio y el guardado de la E13 (B1 y B4)",
        [FormCatalogErrors.FieldInvalidCode] = "los campos llegan con el dominio y el guardado de la E13 (B1 y B4)",
        [FormCatalogErrors.PresentationInvalidCode] = "la presentación llega con el guardado de la E13 (B4)",
        [FormCatalogErrors.FieldRemovalNotConfirmedCode] = "quitar un campo con valores llega en la E13 (B6)",
        [FormCatalogErrors.FieldTypeNotConvertibleCode] = "cambiar el tipo o las reglas llega en la E13 (B5)",
        [FormCatalogErrors.FieldValuesIncompatibleCode] = "cambiar el tipo o las reglas llega en la E13 (B5)",
        [FormCatalogErrors.FieldValuesMissingCode] = "cambiar el tipo o las reglas llega en la E13 (B5)",
        [FormCatalogErrors.FieldValueInvalidCode] = "los valores de los campos llegan en la E13 (B3 y B4)",
        [FormCatalogErrors.FieldValueRequiredCode] = "los valores de los campos llegan en la E13 (B3 y B4)",
        [FormCatalogErrors.FieldValueDuplicateCode] = "los valores de los campos llegan en la E13 (B3 y B4)",
        [FormCatalogErrors.SourceInUseCode] = "proteger una fuente llega en la E13 (B6)",
        ["GET /api/organization/forms/catalogs/template 404 " + FormCatalogErrors.NotFoundCode] = "la plantilla con catalogId llega en la E13 (B7)",
    };

    [Fact]
    public void Every_forms_response_and_business_error_has_an_example_of_a_real_run()
    {
        using var openApi = Read("openapi.json");
        using var examples = Read("examples", ExamplesFile);

        var missing = Missing(openApi.RootElement, examples.RootElement);

        Assert.True(missing.Length == 0, $"Sin ejemplo en docs/contracts/examples/{ExamplesFile} (correr FormsContractExamplesTests):\n"
            + string.Join('\n', missing));
    }

    [Fact]
    public void Every_forms_example_answers_a_response_that_openapi_declares()
    {
        using var openApi = Read("openapi.json");
        using var examples = Read("examples", ExamplesFile);
        var paths = openApi.RootElement.GetProperty("paths");

        var undeclared = examples.RootElement.GetProperty("examples").EnumerateArray()
            .Where(example => !(paths.TryGetProperty(example.GetProperty("path").GetString()!, out var item)
                && item.TryGetProperty(example.GetProperty("method").GetString()!.ToLowerInvariant(), out var operation)
                && operation.GetProperty("responses").TryGetProperty(example.GetProperty("status").GetInt32().ToString(CultureInfo.InvariantCulture), out _)))
            .Select(example => example.GetProperty("name").GetString())
            .ToArray();

        Assert.Empty(undeclared);
    }

    [Fact]
    public void Guard_detects_a_missing_success_and_a_missing_business_error_and_accepts_them_when_recorded()
    {
        using var openApi = JsonDocument.Parse("""
            {"paths":{"/api/organization/forms/catalogs/{id}":{"get":{
              "responses":{"200":{"content":{"application/json":{}}},"404":{"content":{"application/problem+json":{}}}},
              "x-error-codes":[{"status":404,"code":"Http.NotFound"},{"status":404,"code":"Forms.Catalog.NotFound"}]}}}}
            """);
        using var none = JsonDocument.Parse("""{"examples":[]}""");
        using var both = JsonDocument.Parse("""
            {"examples":[
              {"method":"GET","path":"/api/organization/forms/catalogs/{id}","status":200,"body":{}},
              {"method":"GET","path":"/api/organization/forms/catalogs/{id}","status":404,"body":{"code":"Forms.Catalog.NotFound"}}]}
            """);

        Assert.Equal(["GET /api/organization/forms/catalogs/{id} 200", "GET /api/organization/forms/catalogs/{id} 404 Forms.Catalog.NotFound"],
            Missing(openApi.RootElement, none.RootElement));
        Assert.Empty(Missing(openApi.RootElement, both.RootElement));
    }

    private static string[] Missing(JsonElement openApi, JsonElement examples)
    {
        var recorded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var example in examples.GetProperty("examples").EnumerateArray())
        {
            var response = Key(example.GetProperty("method").GetString()!, example.GetProperty("path").GetString()!,
                example.GetProperty("status").GetInt32(), null);
            recorded.Add(response);
            if (example.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.Object && body.TryGetProperty("code", out var code))
                recorded.Add(response + " " + code.GetString());
        }
        var missing = new List<string>();
        foreach (var path in openApi.GetProperty("paths").EnumerateObject().Where(path => path.Name.StartsWith(Prefix, StringComparison.Ordinal)))
            foreach (var operation in path.Value.EnumerateObject())
            {
                var method = operation.Name.ToUpperInvariant();
                foreach (var response in operation.Value.GetProperty("responses").EnumerateObject())
                {
                    if (!response.Name.StartsWith('2') || !response.Value.TryGetProperty("content", out var content)
                        || !content.EnumerateObject().Any(type => type.Name.EndsWith("json", StringComparison.Ordinal))) continue;
                    var key = Key(method, path.Name, int.Parse(response.Name, CultureInfo.InvariantCulture), null);
                    if (!recorded.Contains(key)) missing.Add(key);
                }
                if (!operation.Value.TryGetProperty("x-error-codes", out var errors)) continue;
                foreach (var error in errors.EnumerateArray())
                {
                    var code = error.GetProperty("code").GetString()!;
                    var key = Key(method, path.Name, error.GetProperty("status").GetInt32(), code);
                    if (!IsBusiness(code) || NotReachableYet.ContainsKey(code) || NotReachableYet.ContainsKey(key)) continue;
                    if (!recorded.Contains(key) && !missing.Contains(key, StringComparer.Ordinal)) missing.Add(key);
                }
            }
        return [.. missing];
    }

    private static bool IsBusiness(string code) =>
        code.StartsWith("Forms.", StringComparison.Ordinal) || code is "General.ConcurrencyConflict" or "Validation.Failed";

    private static string Key(string method, string path, int status, string? code) =>
        $"{method} {path} {status}" + (code is null ? "" : " " + code);

    private static JsonDocument Read(params string[] parts) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine([SolutionRoot.FullPath, "docs", "contracts", .. parts])));
}
