using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Application.Models.AssistantActions;

/// <summary>
/// Una acción declarada por su área (ADR 0068 §2): crea, edita o elimina un registro en una pantalla que ya existe y que
/// guarda por una ruta que ya existe. Es un dato: no tiene código que escriba. Cambiar sus campos es una versión nueva.
/// </summary>
public sealed record AssistantActionDefinition
{
    /// <summary>El id sin versión, como <c>users.invite</c>: área y verbo, en minúsculas.</summary>
    public required string Id { get; init; }

    public required int Version { get; init; }

    public required AssistantActionKind Kind { get; init; }

    /// <summary>El requisito de la ruta que guarda, copiado de su atributo.</summary>
    public required AssistantActionRequirement Requirement { get; init; }

    /// <summary>La clave del módulo que prende la acción, si es de un módulo; <c>null</c> en el núcleo.</summary>
    public string? Module { get; init; }

    /// <summary>La ruta que guarda, con su verbo: <c>POST api/users/invitations</c>.</summary>
    public required string SaveRoute { get; init; }

    public required AssistantActionConfirmation Confirmation { get; init; }

    public required AssistantActionScreen Screen { get; init; }

    public AssistantActionEffect Effect { get; init; } = AssistantActionEffect.None;

    /// <summary>Para editar o eliminar: la lectura de la pantalla que encuentra el registro.</summary>
    public AssistantActionTarget? Target { get; init; }

    /// <summary>Los campos salen de la definición del destino (lo armado como dato, 0068 «Consecuencias»): no hay campos fijos.</summary>
    public bool FieldsFromTarget { get; init; }

    public IReadOnlyList<AssistantActionField> Fields { get; init; } = [];

    /// <summary>El id con versión, como <c>users.invite@1</c>: sella la acción y arma sus textos.</summary>
    [JsonIgnore]
    public string Key => $"{Id}@{Version}";
}

/// <summary>La clase de una acción.</summary>
public enum AssistantActionKind
{
    Create,
    Edit,
    Remove,
    /// <summary>Otra, como dar de baja o reactivar.</summary>
    Other,
}

/// <summary>Cómo se confirma: el diálogo, la página sin guardar o la confirmación de quitar de esa pantalla.</summary>
public enum AssistantActionConfirmation
{
    Dialog,
    Page,
    RemoveConfirmation,
}

/// <summary>El efecto fuera del sistema que tiene guardar (lista cerrada): la pantalla lo avisa antes de guardar.</summary>
public enum AssistantActionEffect
{
    None,
    /// <summary>Guardar le manda un mensaje a alguien, como una invitación o un aviso por correo.</summary>
    SendsMessage,
}

/// <summary>La lectura de la pantalla que encuentra el registro de una acción que edita o elimina.</summary>
public sealed record AssistantActionTarget(string ReadRoute);
