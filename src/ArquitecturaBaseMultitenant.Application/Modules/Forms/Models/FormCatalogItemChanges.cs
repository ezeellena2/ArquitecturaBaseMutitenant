namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

/// <summary>
/// Lo que cambia en las opciones de un catálogo a mano en un guardado (C3), ya validado: las opciones nuevas o cambiadas, los
/// movimientos, el orden (224) y las que se borran (129). El índice de cada lista es el del pedido, para el campo del error.
/// </summary>
public sealed record FormCatalogItemChanges(IReadOnlyList<FormCatalogItemInput> Items, IReadOnlyList<FormCatalogMoveInput> Moves,
    FormCatalogItemSortInput? Sort, IReadOnlyList<Guid> DeleteItems)
{
    public static FormCatalogItemChanges Of(CreateFormCatalogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new(request.Items ?? [], [], null, []);
    }

    public static FormCatalogItemChanges Of(UpdateFormCatalogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new(request.Items ?? [], request.Moves ?? [], request.Sort, request.DeleteItems ?? []);
    }
}
