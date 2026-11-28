using System.Text.Json;
using ArquitecturaBaseMultitenant.Domain.Forms;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

/// <summary>
/// El alta de un catálogo. Sin tipo, Text. Uno a mano lleva sus opciones, cada una con su id local; uno de miembros o de
/// contactos, sus filtros y la persona (<see cref="SampleMemberId"/>) o el cliente (<see cref="SampleContactId"/>) de ejemplo,
/// que se guarda en los filtros. Uno a mano puede traer sus campos (<see cref="Fields"/>, E13), que nacen sin valores, y su
/// presentación.
/// </summary>
public sealed record CreateFormCatalogRequest(string? Name, string? Description, FormCatalogSource? Source, FormCatalogValueType? ValueType,
    IReadOnlyList<FormCatalogItemInput>? Items, FormCatalogFiltersInput? Filters, Guid? SampleMemberId, Guid? SampleContactId,
    IReadOnlyList<FormCatalogFieldInput>? Fields = null, FormCatalogPresentationInput? Presentation = null);

/// <summary>
/// Una opción nueva (con <see cref="LocalId"/>) o cambiada (con <see cref="ItemId"/>). El valor llega como texto canónico (C6) y el
/// dominio lo lee con el tipo del catálogo. <see cref="Fields"/> es el delta de los valores de sus campos (E13), por la identidad del
/// campo: uno ausente se conserva y <c>null</c> lo limpia.
/// </summary>
public sealed record FormCatalogItemInput(Guid? ItemId, string? LocalId, string? Code, string? Text, string? Detail, string? Value,
    bool? IsActive, IReadOnlyDictionary<Guid, JsonElement?>? Fields = null);

/// <summary>
/// Un campo de las opciones (E13): uno guardado con <see cref="Id"/> o uno nuevo con <see cref="LocalId"/>. La clave se escribe
/// hasta guardar y después queda fija; <see cref="Min"/> y <see cref="Max"/> van como texto canónico, un número o <c>yyyy-MM-dd</c>.
/// </summary>
public sealed record FormCatalogFieldInput(Guid? Id, string? LocalId, string? Key, string? Name, FormCatalogFieldType? Type,
    Guid? SourceCatalogId, bool? Required, bool? Unique, int? MaxLength, string? Min, string? Max);

/// <summary>Qué arma el título y la descripción en WhatsApp (E13): <c>text</c>, <c>code</c>, <c>value</c>, <c>detail</c> o <c>field:&lt;id&gt;</c>.</summary>
public sealed record FormCatalogPresentationInput(IReadOnlyList<string>? Title, IReadOnlyList<string>? Description);

/// <summary>Los filtros de un catálogo de miembros o de contactos (C5), con la versión de la fuente que se leyó.</summary>
public sealed record FormCatalogFiltersInput(int? SourceVersion, IReadOnlyList<FormCatalogConditionInput>? Conditions,
    bool? ExcludeRecipient, string? Detail, FormCatalogFilterSortInput? Sort);

public sealed record FormCatalogConditionInput(string? Column, string? Operator, IReadOnlyList<string>? Values);

public sealed record FormCatalogFilterSortInput(string? Column, bool? Descending);
