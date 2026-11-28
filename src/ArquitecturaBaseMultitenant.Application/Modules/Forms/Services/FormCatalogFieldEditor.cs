using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;

/// <summary>
/// Cambia los campos de un catálogo a mano, su presentación y los valores de sus opciones (E13), dentro de la transacción del servicio
/// y bajo el bloqueo del catálogo y de sus fuentes, que el servicio toma de una vez
/// (<see cref="IFormCatalogRepository.LockWithSourcesAsync"/>). Toca solo lo que viene en el pedido: la lista de campos y la
/// presentación si vinieron, y el delta de valores de cada opción nueva o cambiada, por la identidad del campo (ausente conserva y
/// <c>null</c> limpia). La regla de cada valor es <see cref="FormCatalogField.Check"/>, con el mensaje de
/// <see cref="FormCatalogFieldValuePolicy"/>; una fuente nueva tiene que ser un catálogo a mano activo de la organización, y un valor
/// único se compara con las opciones activas que quedan. Cambiar el tipo, la fuente o una regla de un campo guardado lo comprueba
/// contra todas las opciones <see cref="FormCatalogFieldChangePolicy"/>. Un error de cualquier paso revierte todo el guardado.
/// </summary>
internal sealed class FormCatalogFieldEditor(IFormCatalogRepository repository, IFormCatalogReader reader, FormCatalogFieldValuePolicy values,
    FormCatalogFieldChangePolicy changes)
{
    private static readonly IReadOnlyDictionary<Guid, JsonElement?> NoDelta = new Dictionary<Guid, JsonElement?>();

    /// <summary>Los catálogos que nombra el pedido como fuente de un campo: se bloquean junto con el catálogo.</summary>
    public static IReadOnlyList<Guid> SourcesOf(IReadOnlyList<FormCatalogFieldInput>? fields) =>
        [.. (fields ?? []).Select(field => field?.SourceCatalogId).OfType<Guid>().Where(id => id != Guid.Empty).Distinct()];

    /// <summary>
    /// Deja los campos de <paramref name="fields"/> (la lista entera de los activos, en su orden) y la presentación, si vinieron, y
    /// devuelve la identidad de cada campo nuevo por su id local. Una fuente que no cambia no se vuelve a leer, y los valores guardados
    /// de un campo que cambió se comprueban teniendo en cuenta <paramref name="items"/>, los cambios de las opciones del mismo guardado.
    /// </summary>
    public async Task<Result<IReadOnlyList<FormCatalogFieldIdentity>>> ChangeAsync(FormCatalog catalog,
        IReadOnlyList<FormCatalogFieldInput>? fields, FormCatalogPresentationInput? presentation, FormCatalogItemChanges items, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        IReadOnlyList<FormCatalogFieldIdentity> identities = [];
        if (fields is not null)
        {
            var changed = await ChangeFieldsAsync(catalog, fields, items, ct);
            if (changed.IsFailure) return changed.Error;
            identities = changed.Value;
        }

        if (presentation is not null)
        {
            var placed = catalog.ChangePresentation(new FormCatalogPresentation(presentation.Title!, presentation.Description!));
            if (placed.IsFailure) return placed.Error;
        }

        return Result.Success(identities);
    }

    /// <summary>
    /// Aplica el delta de valores de cada opción nueva o cambiada, en el orden del pedido: un campo que no está activo, un valor que
    /// no es de su tipo o no cumple su límite, y uno nuevo de «De un catálogo» que no es una opción activa de su fuente son
    /// <see cref="FormCatalogErrors.FieldValueInvalid"/>; el guardado que apunta a una opción dada de baja se conserva (lección 1).
    /// Una opción activa necesita un valor en cada campo obligatorio, también sin delta.
    /// </summary>
    /// <param name="catalog">El catálogo, ya bloqueado y con los campos de este guardado.</param>
    /// <param name="changed">Las opciones nuevas o cambiadas, con su índice en el pedido.</param>
    /// <param name="inputs">Las opciones del pedido, por ese índice.</param>
    /// <param name="ct">El token del pedido.</param>
    public async Task<Result> ApplyValuesAsync(FormCatalog catalog, IReadOnlyList<(int Index, FormCatalogItem Item)> changed,
        IReadOnlyList<FormCatalogItemInput> inputs, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(inputs);
        var active = catalog.ReadFields().Where(field => field.IsActive).ToDictionary(field => field.Id);
        if (active.Count == 0 && changed.All(entry => inputs[entry.Index].Fields is not { Count: > 0 })) return Result.Success();

        DisplayFormatContext? culture = null;
        var options = new List<FormCatalogFieldOptionValue>();
        var owners = new List<(int Index, FormCatalogField Field)>();
        foreach (var (index, item) in changed)
        {
            var delta = inputs[index].Fields ?? NoDelta;
            var before = item.ReadFieldValues();
            foreach (var (fieldId, value) in delta)
            {
                if (!active.TryGetValue(fieldId, out var field))
                    return FormCatalogErrors.FieldValueInvalid(index, fieldId, FormCatalogErrors.FieldValueInvalidCode);
                var checkedValue = field.Check(value ?? default);
                if (checkedValue.Issue is { } issue) return values.ErrorOf(index, field, issue, culture ??= await values.CultureAsync(ct));
                if (field.Type != FormCatalogFieldType.Catalog || checkedValue.Canonical is not { } canonical) continue;
                options.Add(new FormCatalogFieldOptionValue(field, Guid.Parse(canonical.GetString()!), StoredOption(before, field.Key)));
                owners.Add((index, field));
            }

            var set = item.SetFieldValues(catalog, index, delta);
            if (set.IsFailure) return set.Error;
        }

        if (options.Count == 0) return Result.Success();
        var accepted = await values.CheckOptionsAsync(options, ct);
        for (var position = 0; position < accepted.Count; position++)
        {
            if (accepted[position]) continue;
            var (index, field) = owners[position];
            return values.ErrorOf(index, field, FormCatalogFieldValueIssue.Invalid, culture ??= await values.CultureAsync(ct));
        }

        return Result.Success();
    }

    /// <summary>
    /// Un valor único no puede estar en otra opción activa: ni en otra del pedido ni en una guardada que queda. Las que se borran o
    /// cambian en este guardado se miran con lo nuevo; el texto se compara sin mayúsculas ni espacios en los extremos, como el código.
    /// Va con las posiciones ya acomodadas, así el error dice el número de la otra.
    /// </summary>
    public async Task<Result> CheckUniqueAsync(FormCatalog catalog, IReadOnlyList<(int Index, FormCatalogItem Item)> changed,
        IReadOnlyCollection<FormCatalogItem> removed, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(removed);
        var unique = catalog.ReadFields().Where(field => field.IsActive && field.Unique).ToList();
        if (unique.Count == 0 || changed.Count == 0) return Result.Success();

        var released = changed.Select(entry => entry.Item.Id).Concat(removed.Select(item => item.Id)).ToHashSet();
        DisplayFormatContext? culture = null;
        foreach (var field in unique)
        {
            var candidates = new List<(int Index, FormCatalogItem Item, string Value)>();
            foreach (var (index, item) in changed)
                if (item.IsActive && item.ReadFieldValues().TryGetValue(field.Key, out var value) && UniqueKeyOf(value) is { } key)
                    candidates.Add((index, item, key));
            if (candidates.Count == 0) continue;

            var taken = new Dictionary<string, FormCatalogItem>(StringComparer.Ordinal);
            var holders = await repository.ListActiveItemsByFieldValuesAsync(catalog.Id, field.Key,
                [.. candidates.Select(candidate => candidate.Value).Distinct(StringComparer.Ordinal)], ct);
            foreach (var holder in holders.Where(holder => !released.Contains(holder.Id)).OrderBy(holder => holder.Position))
                if (holder.ReadFieldValues().TryGetValue(field.Key, out var held) && UniqueKeyOf(held) is { } heldKey) taken.TryAdd(heldKey, holder);

            foreach (var (index, item, value) in candidates)
            {
                if (taken.TryGetValue(value, out var other))
                    return values.DuplicateErrorOf(index, field, other.Position + 1, culture ??= await values.CultureAsync(ct));
                taken[value] = item;
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Los campos del pedido: una fuente nueva o cambiada tiene que ser otro catálogo a mano activo de la organización (una ajena
    /// se rechaza como una que no existe); lo demás lo decide <see cref="FormCatalog.ChangeFields"/>, y si los valores guardados admiten
    /// el tipo o la regla nueva, <see cref="FormCatalogFieldChangePolicy"/>.
    /// </summary>
    private async Task<Result<IReadOnlyList<FormCatalogFieldIdentity>>> ChangeFieldsAsync(FormCatalog catalog,
        IReadOnlyList<FormCatalogFieldInput> fields, FormCatalogItemChanges items, CancellationToken ct)
    {
        var before = catalog.ReadFields();
        var stored = before.ToDictionary(field => field.Id);
        var drafts = new List<FormCatalogFieldDraft>(fields.Count);
        var sources = new List<(int Index, Guid SourceId)>();
        for (var index = 0; index < fields.Count; index++)
        {
            var input = fields[index];
            drafts.Add(new FormCatalogFieldDraft(input.Id, input.Key, input.Name!, input.Type!.Value, input.SourceCatalogId,
                input.Required!.Value, input.Unique!.Value, input.MaxLength, input.Min, input.Max));
            var kept = input.Id is { } id && stored.TryGetValue(id, out var saved) && saved.SourceCatalogId == input.SourceCatalogId;
            if (input.SourceCatalogId is { } sourceId && !kept) sources.Add((index, sourceId));
        }

        if (sources.Count > 0)
        {
            var found = (await reader.ListFieldSourcesAsync([.. sources.Select(entry => entry.SourceId).Distinct()], ct))
                .ToDictionary(source => source.Id);
            foreach (var (index, sourceId) in sources)
            {
                if (sourceId == catalog.Id || !found.TryGetValue(sourceId, out var source) || source.Source != FormCatalogSource.Manual
                    || !source.IsActive)
                    return FormCatalogErrors.FieldInvalid(index);
            }
        }

        var culture = await values.CultureAsync(ct);
        var active = catalog.ChangeFields(drafts, name => FormCatalogFieldValuePolicy.KeyTakenBy(name, culture));
        if (active.IsFailure) return active.Error;
        var converted = await changes.ApplyAsync(catalog, before, items, ct);
        if (converted.IsFailure) return converted.Error;
        var identities = new List<FormCatalogFieldIdentity>();
        for (var index = 0; index < fields.Count; index++)
            if (fields[index].Id is null) identities.Add(new FormCatalogFieldIdentity(fields[index].LocalId!, active.Value[index].Id));
        return Result.Success<IReadOnlyList<FormCatalogFieldIdentity>>(identities);
    }

    /// <summary>La opción que tenía guardada ese campo, si es una identidad.</summary>
    private static Guid? StoredOption(IReadOnlyDictionary<string, JsonElement> before, string key) =>
        before.TryGetValue(key, out var saved) && saved.ValueKind == JsonValueKind.String && Guid.TryParse(saved.GetString(), out var id)
            ? id : null;

    /// <summary>El valor como se compara para «Único»: el texto sin espacios en los extremos y en mayúsculas; vacío no cuenta.</summary>
    internal static string? UniqueKeyOf(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String when value.GetString()!.Trim() is { Length: > 0 } text => text.ToUpperInvariant(),
        JsonValueKind.True => bool.TrueString.ToUpperInvariant(),
        JsonValueKind.False => bool.FalseString.ToUpperInvariant(),
        _ => null,
    };
}
