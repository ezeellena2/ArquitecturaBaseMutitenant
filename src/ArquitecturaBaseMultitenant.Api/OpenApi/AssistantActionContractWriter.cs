using System.Text.Json;
using System.Text.Json.Serialization;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;

namespace ArquitecturaBaseMultitenant.Api.OpenApi;

/// <summary>
/// Publica las acciones declaradas (ADR 0068) en <c>docs/contracts/assistant-actions.json</c>: el exportador de OpenAPI del
/// build lo escribe junto al ensamblado de la Api y <c>CopyVersionedOpenApi</c> lo copia, como <c>openapi.json</c>. El front
/// genera sus tipos con <c>npm run contracts</c>.
/// </summary>
internal static class AssistantActionContractWriter
{
    internal const string FileName = "assistant-actions.json";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        NewLine = "\n",
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    /// <summary>Cada acción con su id, versión, clase, requisito, ruta, confirmación, pantalla, efecto, destino y campos.</summary>
    internal static string Render(IEnumerable<IAssistantActionSource> sources) => JsonSerializer.Serialize(new
    {
        actions = sources.SelectMany(source => source.Definitions)
            .OrderBy(action => action.Id, StringComparer.Ordinal).ThenBy(action => action.Version),
    }, Options) + "\n";

    internal static void Write(IServiceProvider services, string directory)
    {
        using var scope = services.CreateScope();
        File.WriteAllText(Path.Combine(directory, FileName), Render(scope.ServiceProvider.GetServices<IAssistantActionSource>()));
    }
}
