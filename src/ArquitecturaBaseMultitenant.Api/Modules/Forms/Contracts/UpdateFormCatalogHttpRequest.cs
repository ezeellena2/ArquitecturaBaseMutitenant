using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

namespace ArquitecturaBaseMultitenant.Api.Modules.Forms.Contracts;

/// <summary>
/// Guardar todo junto (C3): <c>items</c> lleva solo lo nuevo o cambiado; <c>isActive</c> es obligatorio; <c>moves</c>,
/// <c>sort</c> y <c>deleteItems</c> (hasta 500, sin repetir) no se combinan entre sí. Desde la E13, <c>fields</c> es la lista entera
/// de los campos activos si cambió, en su orden; <c>confirmedFieldRemovals</c>, los campos con valores que se quitan, y
/// <c>presentation</c>, qué va en WhatsApp.
/// </summary>
public sealed record UpdateFormCatalogHttpRequest(string? Name, string? Description, bool? IsActive, uint? Version,
    IReadOnlyList<FormCatalogItemHttpInput>? Items, IReadOnlyList<FormCatalogMoveHttpInput>? Moves, FormCatalogItemSortHttpInput? Sort,
    IReadOnlyList<Guid>? DeleteItems, FormCatalogFiltersHttpInput? Filters, Guid? SampleMemberId, Guid? SampleContactId,
    IReadOnlyList<FormCatalogFieldHttpInput>? Fields, FormCatalogPresentationHttpInput? Presentation, IReadOnlyList<Guid>? ConfirmedFieldRemovals);

public sealed record FormCatalogMoveHttpInput(Guid? ItemId, FormCatalogMoveDirection? Direction);

/// <summary>«Ordenar por…» (224): <c>column</c> es <c>Text</c>, <c>Code</c> o, desde la E13, la identidad de un campo activo guardado.</summary>
public sealed record FormCatalogItemSortHttpInput(string? Column, bool? Descending);
