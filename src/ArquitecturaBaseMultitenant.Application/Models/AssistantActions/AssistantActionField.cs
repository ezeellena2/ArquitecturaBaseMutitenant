using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Application.Models.AssistantActions;

/// <summary>
/// Un campo de la pantalla, no del pedido HTTP (ADR 0068 §4): lo que la persona ve, con su tipo de la lista cerrada y su
/// límite. Al editar, una lista se cambia con «sumar» y «quitar», nunca entera.
/// </summary>
public sealed record AssistantActionField
{
    /// <summary>El nombre del campo en la pantalla, en camelCase: arma su texto <c>Action.&lt;id&gt;@&lt;versión&gt;.Field.&lt;nombre&gt;</c>.</summary>
    public required string Name { get; init; }

    public required AssistantActionFieldType Type { get; init; }

    public bool Required { get; init; }

    /// <summary>El largo máximo de un texto.</summary>
    public int? MaxLength { get; init; }

    /// <summary>El tope de elementos de un <c>Choices</c> o de filas de un <c>Items</c>.</summary>
    public int? MaxItems { get; init; }

    /// <summary>El mínimo de un número.</summary>
    public decimal? Minimum { get; init; }

    /// <summary>El máximo de un número.</summary>
    public decimal? Maximum { get; init; }

    /// <summary>El campo del que depende, como los roles de la empresa elegida.</summary>
    public string? DependsOn { get; init; }

    /// <summary>En un <c>Items</c>, el campo que identifica cada fila.</summary>
    public string? ItemKey { get; init; }

    /// <summary>En un <c>Items</c>, los campos de cada fila: de esta misma lista, pero nunca otro <c>Items</c>.</summary>
    public IReadOnlyList<AssistantActionField>? ItemFields { get; init; }
}

/// <summary>
/// Los tipos de campo (lista cerrada). No hay tipo para claves, conexiones, SQL, expresiones ni URL.
/// </summary>
public enum AssistantActionFieldType
{
    Text,
    LongText,
    /// <summary>El <c>Integer</c> de la ADR 0068: un número entero.</summary>
    [JsonStringEnumMemberName("integer")] WholeNumber,
    /// <summary>El <c>Decimal</c> de la ADR 0068: un número con decimales.</summary>
    [JsonStringEnumMemberName("decimal")] DecimalNumber,
    Money,
    Date,
    Boolean,
    Email,
    Phone,
    TaxId,
    /// <summary>Una opción de una definición: un rol, un formulario, un catálogo.</summary>
    Choice,
    /// <summary>Varias opciones de una definición.</summary>
    Choices,
    /// <summary>Un registro: una empresa, una persona, una opción de un catálogo. Lo escribe el modelo como texto y lo resuelve el servidor.</summary>
    Record,
    /// <summary>Un consentimiento: nunca se propone y llega sin tildar.</summary>
    Consent,
    /// <summary>Una lista de filas con sus campos, de un solo nivel, con tope y clave por fila.</summary>
    Items,
}
