using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

namespace ArquitecturaBaseMultitenant.Api.Modules.Forms.Contracts;

/// <summary>
/// Guardar todo junto (C3): <c>items</c> lleva solo lo nuevo o cambiado; <c>isActive</c> es obligatorio; <c>moves</c>,
/// <c>sort</c> y <c>deleteItems</c> (hasta 500, sin repetir) no se combinan entre sí.
/// </summary>
public sealed record UpdateFormCatalogHttpRequest(string? Name, string? Description, bool? IsActive, uint? Version,
    IReadOnlyList<FormCatalogItemHttpInput>? Items, IReadOnlyList<FormCatalogMoveHttpInput>? Moves, FormCatalogItemSortHttpInput? Sort,
    IReadOnlyList<Guid>? DeleteItems, FormCatalogFiltersHttpInput? Filters, Guid? SampleMemberId, Guid? SampleContactId);

public sealed record FormCatalogMoveHttpInput(Guid? ItemId, FormCatalogMoveDirection? Direction);

public sealed record FormCatalogItemSortHttpInput(FormCatalogItemSortColumn? Column, bool? Descending);
