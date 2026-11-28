using System.Globalization;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;

/// <summary>
/// Acomoda las posiciones de las opciones de un catálogo a mano bajo su bloqueo (C4): la posición la maneja el servidor y es
/// única por catálogo, con la restricción diferida de respaldo. Cambia el agregado; las reglas que solo devuelven un error son de
/// <see cref="FormCatalogPositionGuard"/>.
/// </summary>
internal sealed class FormCatalogPositionEditor(IFormCatalogRepository repository, IFormCatalogReader reader)
{
    /// <summary>Las opciones de una fuente que se leen por consulta, para ordenar por el texto de su opción.</summary>
    private const int OptionsPerRead = 1_000;

    /// <summary>
    /// Deja cada opción en su lugar. Sin borrados, movimientos ni orden, las nuevas van al final y nada más se toca, así guardar una
    /// opción entre 10.000 escribe solo esa. Si no, arma el orden global por posición e id: las borradas salen, las nuevas van al
    /// final, los movimientos se aplican en orden intercambiando con la vecina (un movimiento fuera de rango revierte todo el
    /// guardado) y «Ordenar por…» reordena todas, también las de otras páginas y las dadas de baja (224). Al renumerar, las demás
    /// cierran el hueco de las borradas (flujo 2 de la §5), y solo cambia la posición de las que se corrieron.
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
            var sorted = await SortAsync(catalog, order, sort, ct);
            if (sorted.IsFailure) return sorted.Error;
            order = [.. sorted.Value];
        }

        for (var position = 0; position < order.Count; position++)
        {
            if (order[position].Position != position) order[position].PlaceAt(position);
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

    /// <summary>
    /// «Ordenar por…» (224) el texto, el código o, desde la E13, un campo activo guardado; otra cosa da 400 en <c>sort.column</c>.
    /// Cada tipo compara en el suyo: un número de menor a mayor, una fecha de la más antigua a la más reciente, sí o no con «No»
    /// primero, uno de otro catálogo por el texto de su opción (aunque esté dada de baja) y un texto como el texto de la opción.
    /// Lo vacío va al final en los dos sentidos, y los empates conservan su lugar.
    /// </summary>
    private async Task<Result<IReadOnlyList<FormCatalogItem>>> SortAsync(FormCatalog catalog, List<FormCatalogItem> order,
        FormCatalogItemSortInput sort, CancellationToken ct)
    {
        var descending = sort.Descending ?? false;
        var culture = CultureInfo.CurrentCulture;
        if (sort.FixedColumn is { } column)
        {
            Func<FormCatalogItem, string?> text = column == FormCatalogItemSortColumn.Code ? item => item.Code : item => item.Text;
            return Result.Success<IReadOnlyList<FormCatalogItem>>([.. SortByText(order, text, descending, culture)]);
        }

        var field = catalog.ReadFields().FirstOrDefault(candidate => candidate.Id == sort.FieldId && candidate.IsActive);
        if (field is null)
            return new ValidationError(new Dictionary<string, string[]> { ["sort.column"] = [ValidationTexts.Required] });

        var values = new Dictionary<Guid, JsonElement>(order.Count);
        foreach (var item in order)
            if (item.ReadFieldValues().TryGetValue(field.Key, out var value)) values[item.Id] = value;
        string? Text(FormCatalogItem item) => values.TryGetValue(item.Id, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

        IEnumerable<FormCatalogItem> sorted;
        switch (field.Type)
        {
            case FormCatalogFieldType.WholeNumber or FormCatalogFieldType.DecimalNumber:
                sorted = SortBy(order, item => Text(item) is { } number
                    && FormCatalogValueText.TryParseNumber(number, wholeNumber: false, out var parsed) ? parsed : (decimal?)null, descending);
                break;
            case FormCatalogFieldType.Date:
                sorted = SortBy(order, item => Text(item) is { } date && FormCatalogValueText.TryParseDate(date, out var parsed)
                    ? parsed : (DateOnly?)null, descending);
                break;
            case FormCatalogFieldType.YesNo:
                sorted = SortBy(order, item => values.TryGetValue(item.Id, out var value)
                    && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : (bool?)null, descending);
                break;
            case FormCatalogFieldType.Catalog:
                var texts = await OptionTextsAsync(field.SourceCatalogId!.Value, order.Select(Text).OfType<string>(), ct);
                sorted = SortByText(order, item => Text(item) is { } option && Guid.TryParse(option, out var id) ? texts.GetValueOrDefault(id) : null,
                    descending, culture);
                break;
            default:
                sorted = SortByText(order, Text, descending, culture);
                break;
        }

        return Result.Success<IReadOnlyList<FormCatalogItem>>([.. sorted]);
    }

    /// <summary>Un orden estable por un valor que se compara en su tipo, con lo vacío al final en los dos sentidos (224).</summary>
    internal static IEnumerable<T> SortBy<T, TKey>(IEnumerable<T> rows, Func<T, TKey?> key, bool descending)
        where TKey : struct, IComparable<TKey>
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(key);
        var withEmptyLast = rows.OrderBy(row => key(row) is null);
        return descending
            ? withEmptyLast.ThenByDescending(row => key(row) ?? default)
            : withEmptyLast.ThenBy(row => key(row) ?? default);
    }

    /// <summary>El texto de cada opción de la fuente que nombran los valores, activa o dada de baja, leído por tandas.</summary>
    private async Task<Dictionary<Guid, string>> OptionTextsAsync(Guid sourceCatalogId, IEnumerable<string> optionIds, CancellationToken ct)
    {
        var texts = new Dictionary<Guid, string>();
        var ids = optionIds.Select(text => Guid.TryParse(text, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).Distinct();
        foreach (var chunk in ids.Chunk(OptionsPerRead))
            foreach (var option in await reader.ListItemsByIdsAsync(sourceCatalogId, chunk, ct)) texts[option.Id] = option.Text;
        return texts;
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
