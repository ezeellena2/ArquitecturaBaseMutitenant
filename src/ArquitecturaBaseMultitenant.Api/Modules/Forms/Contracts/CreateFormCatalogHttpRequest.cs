using System.Text.Json;
using ArquitecturaBaseMultitenant.Domain.Forms;

namespace ArquitecturaBaseMultitenant.Api.Modules.Forms.Contracts;

/// <summary>
/// El alta. Sin tipo, Text. Un catálogo a mano lleva sus opciones, con un id local cada una; uno de miembros o de contactos, sus
/// filtros y la persona (<c>sampleMemberId</c>) o el cliente (<c>sampleContactId</c>) de ejemplo, que se guarda en los filtros. Uno
/// a mano puede traer sus campos (E13), que nacen sin valores, y su presentación.
/// </summary>
public sealed record CreateFormCatalogHttpRequest(string? Name, string? Description, FormCatalogSource? Source, FormCatalogValueType? ValueType,
    IReadOnlyList<FormCatalogItemHttpInput>? Items, FormCatalogFiltersHttpInput? Filters, Guid? SampleMemberId, Guid? SampleContactId,
    IReadOnlyList<FormCatalogFieldHttpInput>? Fields, FormCatalogPresentationHttpInput? Presentation);

/// <summary>
/// Una opción nueva (con <c>localId</c>) o cambiada (con <c>itemId</c>); el valor va como texto canónico (C6). <c>fields</c> es el
/// delta de los valores de sus campos (E13), por la identidad del campo: ausente se conserva y <c>null</c> limpia; un número va
/// como texto, una fecha <c>yyyy-MM-dd</c>, sí/no como booleano y una fuente como la identidad de su opción.
/// </summary>
public sealed record FormCatalogItemHttpInput(Guid? ItemId, string? LocalId, string? Code, string? Text, string? Detail, string? Value,
    bool? IsActive, IReadOnlyDictionary<Guid, JsonElement?>? Fields)
{
    public override string ToString() => nameof(FormCatalogItemHttpInput);
}

/// <summary>
/// Un campo de las opciones (E13): uno guardado con <c>id</c> o uno nuevo con <c>localId</c>; la clave queda fija al guardar, y
/// <c>min</c> y <c>max</c> van como texto canónico, un número o <c>yyyy-MM-dd</c>.
/// </summary>
public sealed record FormCatalogFieldHttpInput(Guid? Id, string? LocalId, string? Key, string? Name, FormCatalogFieldType? Type,
    Guid? SourceCatalogId, bool? Required, bool? Unique, int? MaxLength, string? Min, string? Max);

/// <summary>Qué arma el título y la descripción en WhatsApp (E13): <c>text</c>, <c>code</c>, <c>value</c>, <c>detail</c> o <c>field:&lt;id&gt;</c>.</summary>
public sealed record FormCatalogPresentationHttpInput(IReadOnlyList<string>? Title, IReadOnlyList<string>? Description);

/// <summary>Los filtros de un catálogo de miembros o de contactos (C5), con la versión de la fuente que se leyó.</summary>
public sealed record FormCatalogFiltersHttpInput(int? SourceVersion, IReadOnlyList<FormCatalogConditionHttpInput>? Conditions,
    bool? ExcludeRecipient, string? Detail, FormCatalogFilterSortHttpInput? Sort);

public sealed record FormCatalogConditionHttpInput(string? Column, string? Operator, IReadOnlyList<string>? Values);

public sealed record FormCatalogFilterSortHttpInput(string? Column, bool? Descending);
