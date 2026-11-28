namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

/// <summary>
/// Guardar todo junto (C3), con la versión del catálogo. <see cref="Items"/> lleva solo lo nuevo o cambiado; <see cref="IsActive"/>
/// es obligatorio, así omitirlo no apaga nada; <see cref="Moves"/> y <see cref="DeleteItems"/> llevan hasta 500 cada uno, y
/// <see cref="Sort"/> no se combina con ninguno de los dos (224). Desde la E13, <see cref="Fields"/> es la lista entera de los campos
/// activos si cambió, en su orden; <see cref="ConfirmedFieldRemovals"/>, los campos con valores que se quitan, y
/// <see cref="Presentation"/>, qué va en WhatsApp.
/// </summary>
public sealed record UpdateFormCatalogRequest(Guid CatalogId, string? Name, string? Description, bool? IsActive, uint? Version,
    IReadOnlyList<FormCatalogItemInput>? Items, IReadOnlyList<FormCatalogMoveInput>? Moves, FormCatalogItemSortInput? Sort,
    IReadOnlyList<Guid>? DeleteItems, FormCatalogFiltersInput? Filters, Guid? SampleMemberId, Guid? SampleContactId,
    IReadOnlyList<FormCatalogFieldInput>? Fields = null, FormCatalogPresentationInput? Presentation = null,
    IReadOnlyList<Guid>? ConfirmedFieldRemovals = null)
{
    /// <summary>Si el pedido cambia opciones: altas, cambios, movimientos, el orden o borrados.</summary>
    public bool ChangesItems => Items is { Count: > 0 } || Moves is { Count: > 0 } || Sort is not null || DeleteItems is { Count: > 0 };
}

/// <summary>Subir o bajar una opción: intercambia su posición con la vecina, en el orden del pedido (C4).</summary>
public sealed record FormCatalogMoveInput(Guid? ItemId, FormCatalogMoveDirection? Direction);

/// <summary>
/// «Ordenar por…» (224), que reordena todas las opciones al guardar: <see cref="Column"/> es <c>Text</c>, <c>Code</c> o, desde la
/// E13, la identidad de un campo activo guardado. Es un texto para que la versión anterior de la pantalla siga mandando lo mismo (0060).
/// </summary>
public sealed record FormCatalogItemSortInput(string? Column, bool? Descending)
{
    public FormCatalogItemSortInput(FormCatalogItemSortColumn Column, bool? Descending) : this(Column.ToString(), Descending)
    {
    }

    /// <summary>La columna fija que nombra <see cref="Column"/>, sin distinguir mayúsculas; <c>null</c> si nombra un campo u otra cosa.</summary>
    public FormCatalogItemSortColumn? FixedColumn =>
        string.Equals(Column, nameof(FormCatalogItemSortColumn.Text), StringComparison.OrdinalIgnoreCase) ? FormCatalogItemSortColumn.Text
        : string.Equals(Column, nameof(FormCatalogItemSortColumn.Code), StringComparison.OrdinalIgnoreCase) ? FormCatalogItemSortColumn.Code
        : null;

    /// <summary>La identidad del campo que nombra <see cref="Column"/>; <c>null</c> si es una columna fija o no es una identidad.</summary>
    public Guid? FieldId => FixedColumn is null && Guid.TryParse(Column, out var id) && id != Guid.Empty ? id : null;
}
