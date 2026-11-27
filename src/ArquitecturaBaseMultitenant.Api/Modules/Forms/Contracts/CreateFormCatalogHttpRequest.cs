using ArquitecturaBaseMultitenant.Domain.Forms;

namespace ArquitecturaBaseMultitenant.Api.Modules.Forms.Contracts;

/// <summary>
/// El alta. Sin tipo, Text. Un catálogo a mano lleva sus opciones, con un id local cada una; uno de miembros o de contactos, sus
/// filtros y la persona (<c>sampleMemberId</c>) o el cliente (<c>sampleContactId</c>) de ejemplo, que se guarda en los filtros.
/// </summary>
public sealed record CreateFormCatalogHttpRequest(string? Name, string? Description, FormCatalogSource? Source, FormCatalogValueType? ValueType,
    IReadOnlyList<FormCatalogItemHttpInput>? Items, FormCatalogFiltersHttpInput? Filters, Guid? SampleMemberId, Guid? SampleContactId);

/// <summary>Una opción nueva (con <c>localId</c>) o cambiada (con <c>itemId</c>); el valor va como texto canónico (C6).</summary>
public sealed record FormCatalogItemHttpInput(Guid? ItemId, string? LocalId, string? Code, string? Text, string? Detail, string? Value,
    bool? IsActive)
{
    public override string ToString() => nameof(FormCatalogItemHttpInput);
}

/// <summary>Los filtros de un catálogo de miembros o de contactos (C5), con la versión de la fuente que se leyó.</summary>
public sealed record FormCatalogFiltersHttpInput(int? SourceVersion, IReadOnlyList<FormCatalogConditionHttpInput>? Conditions,
    bool? ExcludeRecipient, string? Detail, FormCatalogFilterSortHttpInput? Sort);

public sealed record FormCatalogConditionHttpInput(string? Column, string? Operator, IReadOnlyList<string>? Values);

public sealed record FormCatalogFilterSortHttpInput(string? Column, bool? Descending);
