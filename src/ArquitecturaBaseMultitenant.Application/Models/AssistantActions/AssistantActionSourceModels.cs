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
    AssistantActionDefinition Action, Guid? TargetId, uint? BaseVersion, IReadOnlyList<AssistantActionResolvedValue> Values);

/// <summary>Una página de opciones de un <c>Choice</c> o un <c>Choices</c>, con la búsqueda y el valor del campo del que depende.</summary>
public sealed record AssistantActionChoiceRequest(
    AssistantActionDefinition Action, string Field, string? Search, int Page, string? DependsOnValue);

/// <summary>Una opción de una definición: su id y su nombre.</summary>
public sealed record AssistantActionChoiceRow(string Id, string Name);
