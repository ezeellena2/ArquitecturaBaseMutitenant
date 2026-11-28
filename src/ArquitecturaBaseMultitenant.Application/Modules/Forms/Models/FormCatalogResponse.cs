using ArquitecturaBaseMultitenant.Domain.Forms;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

/// <summary>
/// El detalle de un catálogo. <see cref="Roles"/> y <see cref="Companies"/> traen el nombre de cada id que usa el filtro, así
/// quien solo lo ve lo lee sin pedir esas listas; un id que ya no existe no viene. <see cref="Sample"/> es la persona o el
/// cliente de ejemplo guardado en los filtros. Desde la E13, <see cref="Fields"/> son los campos de las opciones en su orden,
/// también los quitados (vacío si no es a mano), <see cref="Presentation"/> lo que arma el título y la descripción en WhatsApp, y
/// <see cref="PresentationIssues"/> las opciones activas que no entran.
/// </summary>
public sealed record FormCatalogResponse(Guid Id, string Name, string? Description, FormCatalogSource Source, FormCatalogValueType ValueType,
    FormCatalogFilters? Filters, IReadOnlyList<FormCatalogReference>? Roles, IReadOnlyList<FormCatalogReference>? Companies,
    FormCatalogSample? Sample, bool IsActive, bool Usable, FormCatalogUnusableReason? UnusableReason, bool CanManage,
    FormCatalogItemCounts ItemCounts, DateTime ModifiedAtUtc, string? ModifiedBy, uint Version, IReadOnlyList<FormCatalogFieldDetail> Fields,
    FormCatalogPresentationDetail Presentation, FormCatalogPresentationIssues PresentationIssues);

/// <summary>
/// Un campo de las opciones (E13): su identidad, su clave fija, su nombre, su tipo y sus reglas. <see cref="SourceCatalogId"/> y
/// <see cref="SourceCatalogName"/> son el catálogo de «De un catálogo»; <see cref="Min"/> y <see cref="Max"/>, el límite de un
/// número (como texto, para no perder precisión) o de una fecha (<c>yyyy-MM-dd</c>); <see cref="ValuesCount"/>, las opciones con valor.
/// </summary>
public sealed record FormCatalogFieldDetail(Guid Id, string Key, string Name, FormCatalogFieldType Type, Guid? SourceCatalogId,
    string? SourceCatalogName, bool Required, bool Unique, int? MaxLength, string? Min, string? Max, bool IsActive, int ValuesCount);

/// <summary>
/// Qué arma el título y la descripción de cada opción en WhatsApp (E13): cada uno es una lista de lugares, unidos con « · »; un
/// lugar es <c>text</c>, <c>code</c>, <c>value</c>, <c>detail</c> o <c>field:&lt;id&gt;</c>.
/// </summary>
public sealed record FormCatalogPresentationDetail(IReadOnlyList<string> Title, IReadOnlyList<string> Description)
{
    /// <summary>La de un catálogo sin presentación elegida: el texto en el título y el detalle en la descripción.</summary>
    public static FormCatalogPresentationDetail Default { get; } = new(["text"], ["detail"]);
}

/// <summary>Cuántas opciones activas no entran en WhatsApp y hasta tres de ejemplo, en orden de posición (E13).</summary>
public sealed record FormCatalogPresentationIssues(int Count, IReadOnlyList<FormCatalogPresentationExample> Examples)
{
    public static FormCatalogPresentationIssues None { get; } = new(0, []);
}

public sealed record FormCatalogPresentationExample(Guid ItemId, string Text, FormCatalogPresentationIssue Issue);

/// <summary>Los filtros de un catálogo de miembros o de contactos (C5): condiciones unidas con «y».</summary>
public sealed record FormCatalogFilters(int SourceVersion, IReadOnlyList<FormCatalogCondition> Conditions, bool ExcludeRecipient,
    string? Detail, FormCatalogFilterSort? Sort);

/// <summary>Una condición <c>{column, operator, values[]}</c>, con columna y operador de la fuente (§4 y §4b).</summary>
public sealed record FormCatalogCondition(string Column, string Operator, IReadOnlyList<string> Values);

public sealed record FormCatalogFilterSort(string Column, bool Descending);

/// <summary>Un id con su nombre: un rol o una empresa del filtro, o una fila de esas listas.</summary>
public sealed record FormCatalogReference(Guid Id, string Name);

public sealed record FormCatalogSample(Guid Id, string Name, string? Detail);

public sealed record FormCatalogItemCounts(int Total, int Active);

/// <summary>El id del servidor de cada opción nueva, por el id local con que la mandó la pantalla.</summary>
public sealed record FormCatalogItemIdentity(string LocalId, Guid ItemId);

/// <summary>El alta: el detalle y el mapa de ids de las opciones nuevas.</summary>
public sealed record FormCatalogCreatedResponse(FormCatalogResponse Catalog, IReadOnlyList<FormCatalogItemIdentity> ItemIdentityMap);

/// <summary>El id del servidor de cada campo nuevo, por el id local con que lo mandó la pantalla (E13).</summary>
public sealed record FormCatalogFieldIdentity(string LocalId, Guid FieldId);

/// <summary>Guardar: la versión nueva del catálogo y los mapas de ids de las opciones y de los campos nuevos (C3, E13).</summary>
public sealed record FormCatalogUpdatedResponse(uint Version, IReadOnlyList<FormCatalogItemIdentity> ItemIdentityMap,
    IReadOnlyList<FormCatalogFieldIdentity> FieldIdentityMap);
