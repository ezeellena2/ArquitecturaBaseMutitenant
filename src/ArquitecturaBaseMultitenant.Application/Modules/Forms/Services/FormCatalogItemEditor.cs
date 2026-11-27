using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;

/// <summary>
/// Cambia las opciones de un catálogo a mano dentro de la transacción del servicio y bajo el bloqueo del catálogo (C3 y C4). Toca
/// solo lo que viene en el pedido: las que se borran, las cambiadas y las nuevas; el tope y el código único los protege
/// <see cref="FormCatalogPositionGuard"/>, y el orden lo acomoda <see cref="FormCatalogPositionEditor"/>. Un error de cualquier paso
/// revierte todo el guardado.
/// </summary>
internal sealed class FormCatalogItemEditor(IFormCatalogRepository repository, FormCatalogPositionGuard guard,
    FormCatalogPositionEditor positions)
{
    /// <summary>Aplica los cambios y devuelve el id del servidor de cada opción nueva, por su id local.</summary>
    public async Task<Result<IReadOnlyList<FormCatalogItemIdentity>>> ApplyAsync(FormCatalog catalog, FormCatalogItemChanges changes,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(changes);
        var allowed = catalog.EnsureItemsCanChange();
        if (allowed.IsFailure) return allowed.Error;

        var removed = await ReadRemovedAsync(catalog, changes.DeleteItems, ct);
        if (removed.IsFailure) return removed.Error;
        var edited = await EditAsync(catalog, changes.Items, ct);
        if (edited.IsFailure) return edited.Error;
        var created = Create(catalog, changes.Items);
        if (created.IsFailure) return created.Error;

        var newItems = created.Value.Select(entry => entry.Item).ToList();
        var capacity = await guard.CheckCapacityAsync(catalog, removed.Value.Count, newItems.Count, ct);
        if (capacity.IsFailure) return capacity.Error;
        var arranged = await positions.ArrangeAsync(catalog, removed.Value, newItems, changes.Moves, changes.Sort, ct);
        if (arranged.IsFailure) return arranged.Error;
        var changed = edited.Value.Concat(created.Value.Select(entry => (entry.Index, entry.Item))).OrderBy(entry => entry.Index).ToList();
        var codes = await guard.CheckCodesAsync(catalog, changed, removed.Value, ct);
        if (codes.IsFailure) return codes.Error;

        foreach (var item in removed.Value) repository.Remove(item);
        foreach (var item in newItems) repository.Add(item);
        return Result.Success<IReadOnlyList<FormCatalogItemIdentity>>(
            [.. created.Value.Select(entry => new FormCatalogItemIdentity(entry.LocalId, entry.Item.Id))]);
    }

    /// <summary>
    /// Las opciones por borrar (129): cada una tiene que ser de este catálogo. En esta etapa nada usa una opción; la etapa que sume
    /// un uso lo lee acá, bajo el bloqueo, y responde <see cref="FormCatalogErrors.ItemInUse"/> sin borrar ninguna.
    /// </summary>
    private async Task<Result<IReadOnlyList<FormCatalogItem>>> ReadRemovedAsync(FormCatalog catalog, IReadOnlyList<Guid> ids,
        CancellationToken ct)
    {
        if (ids.Count == 0) return Result.Success<IReadOnlyList<FormCatalogItem>>([]);
        var found = (await repository.ListItemsAsync(catalog.Id, ids, ct)).ToDictionary(item => item.Id);
        for (var index = 0; index < ids.Count; index++)
        {
            if (!found.ContainsKey(ids[index])) return FormCatalogErrors.ItemNotFound($"deleteItems.{index}");
        }

        return Result.Success<IReadOnlyList<FormCatalogItem>>([.. found.Values]);
    }

    /// <summary>Las cambiadas: se escriben y auditan solo si algún valor cambió.</summary>
    private async Task<Result<IReadOnlyList<(int Index, FormCatalogItem Item)>>> EditAsync(FormCatalog catalog,
        IReadOnlyList<FormCatalogItemInput> items, CancellationToken ct)
    {
        var ids = items.Select(input => input.ItemId).OfType<Guid>().ToList();
        if (ids.Count == 0) return Result.Success<IReadOnlyList<(int, FormCatalogItem)>>([]);
        var found = (await repository.ListItemsAsync(catalog.Id, ids, ct)).ToDictionary(item => item.Id);
        var edited = new List<(int Index, FormCatalogItem Item)>(ids.Count);
        for (var index = 0; index < items.Count; index++)
        {
            var input = items[index];
            if (input.ItemId is not { } itemId) continue;
            if (!found.TryGetValue(itemId, out var item)) return FormCatalogErrors.ItemNotFound($"items.{index}.itemId");
            var result = item.Edit(catalog, index, input.Code!, input.Text!, input.Detail, input.Value);
            if (result.IsFailure) return result.Error;
            if (input.IsActive!.Value) item.Reactivate();
            else item.Deactivate();
            edited.Add((index, item));
        }

        return Result.Success<IReadOnlyList<(int, FormCatalogItem)>>(edited);
    }

    /// <summary>Las nuevas, todavía sin lugar: <see cref="FormCatalogPositionEditor.ArrangeAsync"/> las pone al final.</summary>
    private static Result<IReadOnlyList<(int Index, string LocalId, FormCatalogItem Item)>> Create(FormCatalog catalog,
        IReadOnlyList<FormCatalogItemInput> items)
    {
        var created = new List<(int Index, string LocalId, FormCatalogItem Item)>();
        for (var index = 0; index < items.Count; index++)
        {
            var input = items[index];
            if (input.ItemId is not null) continue;
            var item = FormCatalogItem.Create(catalog, index, input.Code!, input.Text!, input.Detail, input.Value, input.IsActive!.Value,
                position: 0);
            if (item.IsFailure) return item.Error;
            created.Add((index, input.LocalId!, item.Value));
        }

        return Result.Success<IReadOnlyList<(int, string, FormCatalogItem)>>(created);
    }
}
