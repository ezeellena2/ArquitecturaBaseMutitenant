using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

/// <summary>
/// La propuesta de un turno (ADR 0068 §8 y §9), leída por id desde la pantalla que la confirma. El estado y los nombres se
/// calculan en cada lectura, con los permisos de ese momento y en la empresa de la acción; nunca se guardan. <c>BaseVersion</c>
/// es la versión del registro cuando la IA preparó la propuesta: si difiere de <c>Target.Version</c>, cambió desde entonces.
/// </summary>
public sealed record AssistantProposalResponse(Guid Id, Guid ConversationId, AssistantProposalKind Kind, string Action, int Version,
    AssistantProposalState State, AssistantProposalUnavailableReason? UnavailableReason, IReadOnlyList<AssistantProposalValueResponse> Values,
    AssistantProposalTargetResponse? Target, uint? BaseVersion, string? AppliedLocation);

/// <summary>Un valor propuesto: el campo, el valor resuelto, su nombre leído del servidor y la clave de texto de la duda («Revisá»).</summary>
public sealed record AssistantProposalValueResponse(string Field, JsonElement Value, string? Label, string? Doubt);

/// <summary>El registro al que se refiere una propuesta que edita o elimina, como está ahora.</summary>
public sealed record AssistantProposalTargetResponse(Guid Id, string Label, uint Version);

/// <summary>
/// Lo que el mensaje del historial y del SSE dice de su propuesta, con su estado guardado (nueva, reemplazada o aplicada); lo
/// demás, también «no disponible» y «Sin confirmar», lo calcula el GET de la propuesta.
/// </summary>
public sealed record AssistantMessageProposalResponse(Guid Id, AssistantProposalKind Kind, string Action, int Version, AssistantProposalState State)
{
    public static AssistantMessageProposalResponse From(Guid id, string action, int version, Domain.Assistant.AssistantProposalStatus state) =>
        new(id, AssistantProposalKind.Action, action, version, state switch
        {
            Domain.Assistant.AssistantProposalStatus.Replaced => AssistantProposalState.Replaced,
            Domain.Assistant.AssistantProposalStatus.Applied => AssistantProposalState.Applied,
            _ => AssistantProposalState.New,
        });
}

/// <summary>La clave del intento de guardar desde una propuesta, para el encabezado Idempotency-Key de la ruta que guarda.</summary>
public sealed record AssistantProposalAttemptResponse(string IdempotencyKey);

/// <summary>El aviso de que la pantalla guardó: la versión nueva del registro, al editar.</summary>
public sealed record AssistantProposalAppliedRequest(uint? Version);

public enum AssistantProposalKind
{
    [JsonStringEnumMemberName("action")] Action,
}

public enum AssistantProposalState
{
    [JsonStringEnumMemberName("new")] New,
    [JsonStringEnumMemberName("replaced")] Replaced,
    [JsonStringEnumMemberName("applied")] Applied,
    [JsonStringEnumMemberName("unconfirmed")] Unconfirmed,
    [JsonStringEnumMemberName("unavailable")] Unavailable,
}

/// <summary>Por qué una propuesta ya no está disponible: sin el permiso, sin el módulo, de otra versión, sin destino o sin la cuenta.</summary>
public enum AssistantProposalUnavailableReason
{
    [JsonStringEnumMemberName("permission")] Permission,
    [JsonStringEnumMemberName("module")] Module,
    [JsonStringEnumMemberName("version")] Version,
    [JsonStringEnumMemberName("target")] Target,
    [JsonStringEnumMemberName("subject")] Subject,
}
