using System.Text.Json;

namespace ArquitecturaBaseMultitenant.Application.Models.AssistantActions;

/// <summary>Lo que escribió el modelo para un campo, sin resolver: un texto, una lista o las operaciones de editar.</summary>
public sealed record AssistantActionInput(string Field, JsonElement Value);

/// <summary>Una acción sin resolver: la acción, el destino como lo escribió el modelo y sus valores.</summary>
public sealed record AssistantActionDraft(AssistantActionDefinition Action, string? Target, IReadOnlyList<AssistantActionInput> Values);

/// <summary>Un campo resuelto: lo que escribió el modelo, el id permitido y la duda («Revisá»), nunca un nombre.</summary>
public sealed record AssistantActionResolvedValue(string Field, JsonElement Written, JsonElement? Resolved, string? Doubt);

/// <summary>Una acción resuelta: el destino y la versión que tenía al resolver (editar y eliminar) y cada campo.</summary>
public sealed record AssistantActionResolution(
    AssistantActionDefinition Action, Guid? TargetId, uint? BaseVersion, IReadOnlyList<AssistantActionResolvedValue> Values)
{
    /// <summary>
    /// Las cuentas de las personas que nombra (el destino o un <c>Record</c> de una persona): quedan como sujetos del mensaje, y
    /// si una se da de baja, la propuesta queda «ya no disponible» (ADR 0068 §8). Las conoce solo la fuente.
    /// </summary>
    public IReadOnlyList<Guid> SubjectUserIds { get; init; } = [];
}

/// <summary>El destino de una propuesta como está ahora: su id, su nombre leído del servidor y su versión.</summary>
public sealed record AssistantActionTargetView(Guid Id, string Label, uint Version);

/// <summary>
/// Un valor de una propuesta como lo dibuja la pantalla: un campo simple con su valor (el id, si es una opción o un registro),
/// un elemento de un <c>Choices</c> como <c>{op, id}</c> y una fila de un <c>Items</c> como <c>{op, key, row?}</c>, cada uno
/// con su nombre leído del servidor y su duda.
/// </summary>
public sealed record AssistantActionValueView(string Field, JsonElement Value, string? Label, string? Doubt);

/// <summary>Una propuesta guardada, vuelta a resolver con los permisos de ahora: el destino de ahora y lo que se muestra.</summary>
public sealed record AssistantActionReview(AssistantActionTargetView? Target, IReadOnlyList<AssistantActionValueView> Values);

/// <summary>Una página de opciones de un <c>Choice</c> o un <c>Choices</c>, con la búsqueda y el valor del campo del que depende.</summary>
public sealed record AssistantActionChoiceRequest(
    AssistantActionDefinition Action, string Field, string? Search, int Page, string? DependsOnValue);

/// <summary>Una opción de una definición: su id y su nombre.</summary>
public sealed record AssistantActionChoiceRow(string Id, string Name);

/// <summary>
/// Las claves de duda («Revisá») que publican las fuentes en cada valor resuelto; el front las traduce. Son códigos, no
/// textos: el núcleo no guarda el texto.
/// </summary>
public static class AssistantActionDoubts
{
    /// <summary>
    /// Hay más de una opción parecida al texto: queda la igual, o la primera por nombre, y la persona la revisa. En el destino
    /// no hay duda: con varias personas parecidas, la fuente lo rechaza con esta clave y el modelo pregunta.
    /// </summary>
    public const string Similar = "Assistant.Action.Doubt.Similar";
}
