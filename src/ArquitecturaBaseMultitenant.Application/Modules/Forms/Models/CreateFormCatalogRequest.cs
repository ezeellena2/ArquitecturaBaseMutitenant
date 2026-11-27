using ArquitecturaBaseMultitenant.Domain.Forms;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

/// <summary>
/// El alta de un catálogo. Sin tipo, Text. Uno a mano lleva sus opciones, cada una con su id local; uno de miembros o de
/// contactos, sus filtros y la persona (<see cref="SampleMemberId"/>) o el cliente (<see cref="SampleContactId"/>) de ejemplo,
/// que se guarda en los filtros.
/// </summary>
public sealed record CreateFormCatalogRequest(string? Name, string? Description, FormCatalogSource? Source, FormCatalogValueType? ValueType,
    IReadOnlyList<FormCatalogItemInput>? Items, FormCatalogFiltersInput? Filters, Guid? SampleMemberId, Guid? SampleContactId);

/// <summary>
/// Una opción nueva (con <see cref="LocalId"/>) o cambiada (con <see cref="ItemId"/>). El valor llega como texto canónico (C6) y el
/// dominio lo lee con el tipo del catálogo.
/// </summary>
public sealed record FormCatalogItemInput(Guid? ItemId, string? LocalId, string? Code, string? Text, string? Detail, string? Value,
    bool? IsActive);

/// <summary>Los filtros de un catálogo de miembros o de contactos (C5), con la versión de la fuente que se leyó.</summary>
public sealed record FormCatalogFiltersInput(int? SourceVersion, IReadOnlyList<FormCatalogConditionInput>? Conditions,
    bool? ExcludeRecipient, string? Detail, FormCatalogFilterSortInput? Sort);

public sealed record FormCatalogConditionInput(string? Column, string? Operator, IReadOnlyList<string>? Values);

public sealed record FormCatalogFilterSortInput(string? Column, bool? Descending);
