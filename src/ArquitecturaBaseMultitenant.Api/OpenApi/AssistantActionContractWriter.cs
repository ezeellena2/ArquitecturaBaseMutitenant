using System.Runtime.CompilerServices;
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

    /// <summary>
    /// Lee las fuentes registradas sin construirlas: sus declaraciones son estáticas (el puerto lo pide), y en el build el
    /// exportador no tiene base para los readers que una fuente recibe.
    /// </summary>
    internal static void Write(IServiceCollection services, string directory)
    {
        var sources = services.Where(descriptor => descriptor.ServiceType == typeof(IAssistantActionSource))
            .Select(descriptor => descriptor.ImplementationType
                ?? throw new InvalidOperationException("An action source must be registered by its type."))
            .Select(type => (IAssistantActionSource)RuntimeHelpers.GetUninitializedObject(type))
            .ToArray();
        foreach (var source in sources)
            if (source.Definitions is null)
                throw new InvalidOperationException($"The action source {source.GetType().Name} must declare its actions statically.");
        File.WriteAllText(Path.Combine(directory, FileName), Render(sources));
    }
}
