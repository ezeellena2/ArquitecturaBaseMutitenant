namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

/// <summary>
/// Guardar todo junto (C3), con la versión del catálogo. <see cref="Items"/> lleva solo lo nuevo o cambiado; <see cref="IsActive"/>
/// es obligatorio, así omitirlo no apaga nada; <see cref="Moves"/> y <see cref="DeleteItems"/> llevan hasta 500 cada uno, y
/// <see cref="Sort"/> no se combina con ninguno de los dos (224).
/// </summary>
public sealed record UpdateFormCatalogRequest(Guid CatalogId, string? Name, string? Description, bool? IsActive, uint? Version,
    IReadOnlyList<FormCatalogItemInput>? Items, IReadOnlyList<FormCatalogMoveInput>? Moves, FormCatalogItemSortInput? Sort,
    IReadOnlyList<Guid>? DeleteItems, FormCatalogFiltersInput? Filters, Guid? SampleMemberId, Guid? SampleContactId)
{
    /// <summary>Si el pedido cambia opciones: altas, cambios, movimientos, el orden o borrados.</summary>
    public bool ChangesItems => Items is { Count: > 0 } || Moves is { Count: > 0 } || Sort is not null || DeleteItems is { Count: > 0 };
}

/// <summary>Subir o bajar una opción: intercambia su posición con la vecina, en el orden del pedido (C4).</summary>
public sealed record FormCatalogMoveInput(Guid? ItemId, FormCatalogMoveDirection? Direction);

/// <summary>«Ordenar por…» Texto o Código, que reordena todas las opciones al guardar (224).</summary>
public sealed record FormCatalogItemSortInput(FormCatalogItemSortColumn? Column, bool? Descending);
