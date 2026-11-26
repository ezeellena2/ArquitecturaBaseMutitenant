using System.Text.Json;
using System.Text.Json.Serialization;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

/// <summary>
/// Lo que guarda la columna <c>Values</c> de una propuesta (ADR 0068 §8): el destino como lo escribió el modelo y, por campo,
/// lo que escribió, el id resuelto y la duda. Nunca un nombre resuelto: los nombres se leen de la fuente en cada lectura.
/// </summary>
public sealed record AssistantProposalValues(string? Target, IReadOnlyList<AssistantActionResolvedValue> Values);

/// <summary>
/// Lo que la historia del turno siguiente lleva de una propuesta (0068 §7; 0064 §9 y §10): la acción y lo que escribió el
/// modelo, nunca lo resuelto ni los nombres.
/// </summary>
public sealed record AssistantProposalHistory(
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("target"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Target,
    [property: JsonPropertyName("values")] IReadOnlyList<AssistantProposalHistoryValue> Values)
{
    /// <summary>Como entra en la historia: la llamada que el modelo hizo, aceptada.</summary>
    public string Render() => JsonSerializer.Serialize(new Dictionary<string, object> { ["propose_action"] = this, ["outcome"] = "accepted" },
        AssistantTurnJson.Options);
}

public sealed record AssistantProposalHistoryValue([property: JsonPropertyName("field")] string Field, [property: JsonPropertyName("value")] JsonElement Value);
