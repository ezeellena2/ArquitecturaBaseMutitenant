using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;

/// <summary>
/// Protege las reglas de las opciones de un catálogo a mano bajo su bloqueo, que ya tomó el repositorio al leerlo (C4): el tope de
/// 10.000, la posición que maneja el servidor y el código único sin distinguir mayúsculas. Las dos restricciones diferidas de la
/// base quedan de respaldo: un choque en el commit sería un 500.
/// </summary>
internal sealed class FormCatalogPositionGuard(IFormCatalogRepository repository)
{
    /// <summary>El tope de opciones, activas y dadas de baja, sin las que se borran en este guardado (§3).</summary>
    public async Task<Result> CheckCapacityAsync(FormCatalog catalog, int removed, int created, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (created == 0) return Result.Success();
        var count = await repository.CountItemsAsync(catalog.Id, ct);
        return count - removed + created > FormCatalogLimits.MaxItems ? FormCatalogErrors.TooManyItems : Result.Success();
    }

    /// <summary>
    /// Deja cada opción en su lugar. Sin borrados, movimientos ni orden, las nuevas van al final y nada más se toca, así guardar una
    /// opción entre 10.000 escribe solo esa. Si no, arma el orden global por posición e id: las borradas dejan su hueco, las nuevas
    /// van al final, los movimientos se aplican en orden intercambiando con la vecina (un movimiento fuera de rango revierte todo
    /// el guardado) y «Ordenar por…» reordena todas, también las de otras páginas y las dadas de baja (224). Al renumerar, solo
    /// cambia la posición de las que se movieron.
    /// </summary>
    public async Task<Result> ArrangeAsync(FormCatalog catalog, IReadOnlyCollection<FormCatalogItem> removed,
        IReadOnlyList<FormCatalogItem> created, IReadOnlyList<FormCatalogMoveInput> moves, FormCatalogItemSortInput? sort,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(removed);
        ArgumentNullException.ThrowIfNull(created);
        ArgumentNullException.ThrowIfNull(moves);
        if (removed.Count == 0 && moves.Count == 0 && sort is null)
        {
            var next = (await repository.FindLastPositionAsync(catalog.Id, ct) ?? -1) + 1;
            foreach (var item in created) item.PlaceAt(next++);
            return Result.Success();
        }

        var removedIds = removed.Select(item => item.Id).ToHashSet();
        var order = (await repository.ListAllItemsAsync(catalog.Id, ct))
            .Where(item => !removedIds.Contains(item.Id))
            .OrderBy(item => item.Position).ThenBy(item => item.Id)
            .Concat(created)
            .ToList();
        var moved = Move(order, moves);
        if (moved.IsFailure) return moved;
        if (sort is not null)
        {
            Func<FormCatalogItem, string?> key = sort.Column == FormCatalogItemSortColumn.Code ? item => item.Code : item => item.Text;
            order = [.. SortByText(order, key, sort.Descending ?? false, CultureInfo.CurrentCulture)];
        }

        for (var position = 0; position < order.Count; position++)
        {
            if (order[position].Position != position) order[position].PlaceAt(position);
        }

        return Result.Success();
    }

    /// <summary>
    /// El código de cada opción nueva o cambiada tiene que estar libre: no lo puede tener otra que queda, ni otra del mismo pedido.
    /// Lee solo las que tienen guardado alguno de esos códigos; una que se borra lo libera, y dos del pedido pueden intercambiarlos.
    /// El error dice el número de la opción que ya lo tiene, con las posiciones ya acomodadas.
    /// </summary>
    /// <param name="catalog">El catálogo, ya bloqueado.</param>
    /// <param name="changed">Las opciones nuevas o cambiadas, con su índice en el pedido, en el orden del pedido.</param>
    /// <param name="removed">Las que se borran en este guardado, que liberan su código.</param>
    /// <param name="ct">El token del pedido.</param>
    public async Task<Result> CheckCodesAsync(FormCatalog catalog, IReadOnlyList<(int Index, FormCatalogItem Item)> changed,
        IReadOnlyCollection<FormCatalogItem> removed, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(removed);
        if (changed.Count == 0) return Result.Success();
        var codes = changed.Select(entry => entry.Item.CodeNormalized).ToHashSet(StringComparer.Ordinal);
        var released = changed.Select(entry => entry.Item.Id).Concat(removed.Select(item => item.Id)).ToHashSet();
        var taken = (await repository.ListItemsByCodesAsync(catalog.Id, codes, ct))
            .Where(holder => !released.Contains(holder.Id))
            .ToDictionary(holder => holder.CodeNormalized, StringComparer.Ordinal);
        foreach (var (index, item) in changed)
        {
            if (taken.TryGetValue(item.CodeNormalized, out var other)) return FormCatalogErrors.DuplicateCode(index, other.Position + 1);
            taken[item.CodeNormalized] = item;
        }

        return Result.Success();
    }

    /// <summary>
    /// «Ordenar por…» (224): un orden estable, así los empates conservan su lugar; lo vacío va al final en los dos sentidos, y el
    /// texto se compara con la intercalación del idioma del pedido, sin distinguir mayúsculas y con los números por su valor
    /// («117» antes que «10030»).
    /// </summary>
    internal static IEnumerable<T> SortByText<T>(IEnumerable<T> rows, Func<T, string?> text, bool descending, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(culture);
        var comparer = StringComparer.Create(culture, CompareOptions.IgnoreCase | CompareOptions.NumericOrdering);
        var withEmptyLast = rows.OrderBy(row => string.IsNullOrWhiteSpace(text(row)));
        return descending
            ? withEmptyLast.ThenByDescending(row => text(row) ?? string.Empty, comparer)
            : withEmptyLast.ThenBy(row => text(row) ?? string.Empty, comparer);
    }

    private static Result Move(List<FormCatalogItem> order, IReadOnlyList<FormCatalogMoveInput> moves)
    {
        if (moves.Count == 0) return Result.Success();
        var indexes = new Dictionary<Guid, int>(order.Count);
        for (var index = 0; index < order.Count; index++) indexes[order[index].Id] = index;
        for (var index = 0; index < moves.Count; index++)
        {
            var move = moves[index];
            if (!indexes.TryGetValue(move.ItemId!.Value, out var from)) return FormCatalogErrors.ItemNotFound($"moves.{index}");
            var to = move.Direction == FormCatalogMoveDirection.Up ? from - 1 : from + 1;
            var neighbor = to >= 0 && to < order.Count ? order[to] : null;
            var swapped = order[from].SwapPosition(neighbor);
            if (swapped.IsFailure) return swapped;
            (order[from], order[to]) = (order[to], order[from]);
            indexes[order[from].Id] = from;
            indexes[order[to].Id] = to;
        }

        return Result.Success();
    }
}
