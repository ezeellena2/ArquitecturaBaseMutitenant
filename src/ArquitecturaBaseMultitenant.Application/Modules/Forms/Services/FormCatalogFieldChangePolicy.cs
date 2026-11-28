using System.Globalization;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Resources;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;

/// <summary>
/// Cambiar el tipo, la fuente o una regla de un campo guardado (E13, B5), dentro de la transacción del guardado y bajo el bloqueo del
/// catálogo y de sus fuentes. Lee todas las opciones en lotes y no escribe ninguna hasta saber que todas admiten el cambio, así un
/// error revierte todo:
/// <list type="bullet">
/// <item>al convertir a número, a fecha o a sí o no, el texto guardado se lee con la cultura del pedido, y lo que se lee de dos formas
/// («1.500», «12.345») no convierte; a texto, queda el texto que se ve: el número y la fecha como se leen en la cultura, «Sí» o «No», y
/// el de la opción fuente, aunque esté dada de baja; a «De un catálogo», hace falta una opción activa de la fuente nueva;</item>
/// <item>lo que no convierte o no cumple el límite del campo es <see cref="FormCatalogErrors.FieldTypeNotConvertible"/> si cambió el
/// tipo, y si no, <see cref="FormCatalogErrors.FieldValuesIncompatible"/>, con las primeras 50 opciones en orden de posición, su
/// motivo y cuántas son;</item>
/// <item>obligatorio y único se comprueban sobre las opciones activas: sin valor es <see cref="FormCatalogErrors.FieldValuesMissing"/>,
/// y un valor repetido, <see cref="FormCatalogErrors.FieldValuesIncompatible"/>.</item>
/// </list>
/// Lo que manda el mismo guardado manda: una opción que se borra no cuenta, el estado de una cambiada es el del pedido, y el valor que
/// trae su delta para el campo ya llega en el tipo nuevo y lo valida <see cref="FormCatalogFieldEditor.ApplyValuesAsync"/>; si repite
/// el de una opción guardada, es <see cref="FormCatalogErrors.FieldValueDuplicate"/> de esa opción.
/// </summary>
internal sealed class FormCatalogFieldChangePolicy(IFormCatalogRepository repository, IFormCatalogReader reader,
    FormCatalogFieldValuePolicy values, DisplayFormatter formatter)
{
    /// <summary>Las opciones que se leen por consulta, y las que se cargan juntas para escribir lo convertido.</summary>
    internal const int ItemsPerBatch = 500;

    /// <summary>
    /// Los campos guardados que siguen activos y cambiaron su tipo, su fuente o una regla, con su índice en el pedido: el de su lugar
    /// entre los activos, que quedan en el orden del pedido.
    /// </summary>
    public static IReadOnlyList<FormCatalogFieldChange> ChangesOf(IReadOnlyList<FormCatalogField> before, IReadOnlyList<FormCatalogField> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var stored = before.Where(field => field.IsActive).ToDictionary(field => field.Id);
        var changes = new List<FormCatalogFieldChange>();
        foreach (var next in after.Where(field => field.IsActive))
            if (stored.TryGetValue(next.Id, out var previous) && new FormCatalogFieldChange(next.Position, previous, next) is { Matters: true } change)
                changes.Add(change);
        return changes;
    }

    /// <summary>
    /// Comprueba cada campo que cambió contra todas las opciones y, si todas lo admiten, deja convertidos los valores guardados.
    /// </summary>
    /// <param name="catalog">El catálogo, bloqueado y con los campos de este guardado.</param>
    /// <param name="before">Los campos como estaban guardados, antes de este guardado.</param>
    /// <param name="items">Los cambios de las opciones del mismo guardado.</param>
    /// <param name="ct">El token del pedido.</param>
    public async Task<Result> ApplyAsync(FormCatalog catalog, IReadOnlyList<FormCatalogField> before, FormCatalogItemChanges items,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(items);
        var changes = ChangesOf(before, catalog.ReadFields());
        if (changes.Count == 0) return Result.Success();

        var request = new RequestedItems(items);
        var culture = await values.CultureAsync(ct);
        var reviews = changes.Select(change => new FieldReview(change)).ToList();
        int? afterPosition = null;
        while (true)
        {
            var batch = await reader.ListItemBatchAsync(catalog.Id, afterPosition, ItemsPerBatch, ct);
            List<(FormCatalogStoredItemRow Row, IReadOnlyDictionary<string, JsonElement> Values)> stored =
                [.. batch.Select(row => (row, FormCatalogItem.FieldValuesFromJson(row.FieldValuesJson)))];
            foreach (var review in reviews) await ReviewAsync(review, stored, request, culture, ct);
            if (batch.Count < ItemsPerBatch) break;
            afterPosition = batch[^1].Position;
        }

        foreach (var review in reviews)
            if (Decide(review, request, culture) is { } error) return error;

        await WriteAsync(catalog.Id, reviews, ct);
        return Result.Success();
    }

    /// <summary>Un lote de opciones contra un campo: lo convierte, junta lo que no entra y lo que hace falta para obligatorio y único.</summary>
    private async Task ReviewAsync(FieldReview review, List<(FormCatalogStoredItemRow Row, IReadOnlyDictionary<string, JsonElement> Values)> batch,
        RequestedItems request, DisplayFormatContext culture, CancellationToken ct)
    {
        var change = review.Change;
        var entries = new List<(FormCatalogStoredItemRow Row, bool IsActive, JsonElement? Stored)>(batch.Count);
        foreach (var (row, saved) in batch)
        {
            if (request.Deletes(row.Id) || request.SendsValue(row.Id, change.Next.Id)) continue;
            entries.Add((row, request.IsActive(row), saved.TryGetValue(change.Next.Key, out var value) ? value : (JsonElement?)null));
        }

        var optionTexts = await OptionTextsAsync(change, entries.Select(entry => entry.Stored), ct);
        var converted = entries.Select(entry => Convert(change, entry.Stored, culture, optionTexts)).ToArray();
        await RejectInactiveOptionsAsync(change, converted, ct);

        for (var index = 0; index < entries.Count; index++)
        {
            var (row, isActive, stored) = entries[index];
            var value = converted[index];
            if (value.Issue is { } issue)
            {
                review.Reject(row, ReasonOf(issue));
                continue;
            }

            if (change.Converts && !SameValue(stored, value.Canonical)) review.Converted[row.Id] = value.Canonical;
            if (!isActive) continue;
            if (change.ChecksRequired && IsEmpty(value.Canonical)) review.Missing++;
            if (change.ChecksUnique && value.Canonical is { } canonical && FormCatalogFieldEditor.UniqueKeyOf(canonical) is { } key)
                review.Hold(key, row);
        }
    }

    /// <summary>
    /// El valor guardado en el campo nuevo: sin cambiar el tipo, el mismo con el límite nuevo; a texto, el texto que se ve; desde
    /// texto, leído con la cultura del pedido; y entre los demás, su forma canónica, que solo sirve entre números enteros y con
    /// decimales o entre dos fuentes.
    /// </summary>
    private FormCatalogFieldValue Convert(FormCatalogFieldChange change, JsonElement? stored, DisplayFormatContext culture,
        IReadOnlyDictionary<Guid, string> optionTexts)
    {
        if (stored is not { } value) return FormCatalogFieldValue.Empty;
        var (previous, next) = (change.Previous, change.Next);
        if (!change.Converts) return next.Check(value);
        if (next.Type == FormCatalogFieldType.Text)
            return TextOf(previous, value, culture, optionTexts) is { } text ? next.Check(JsonSerializer.SerializeToElement(text))
                : FormCatalogFieldValue.Failed(FormCatalogFieldValueIssue.Invalid);
        if (previous.Type == FormCatalogFieldType.Text)
            return value.ValueKind == JsonValueKind.String ? FormCatalogFieldValuePolicy.Read(next, value.GetString()!, culture)
                : FormCatalogFieldValue.Failed(FormCatalogFieldValueIssue.Invalid);
        return next.Check(value);
    }

    /// <summary>El texto que se ve de un valor canónico, en la cultura del pedido; <c>null</c> si no se puede leer.</summary>
    private string? TextOf(FormCatalogField previous, JsonElement value, DisplayFormatContext culture, IReadOnlyDictionary<Guid, string> optionTexts)
    {
        switch (previous.Type)
        {
            case FormCatalogFieldType.YesNo when value.ValueKind is JsonValueKind.True or JsonValueKind.False:
                return FormattingTexts.Get(value.GetBoolean() ? "Boolean.True" : "Boolean.False", culture.Profile);
            case FormCatalogFieldType.WholeNumber or FormCatalogFieldType.DecimalNumber when value.ValueKind == JsonValueKind.String
                && decimal.TryParse(value.GetString(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number):
                var canonical = value.GetString()!;
                var point = canonical.IndexOf('.', StringComparison.Ordinal);
                return formatter.FormatDecimal(number, point < 0 ? 0 : canonical.Length - point - 1, culture);
            case FormCatalogFieldType.Date when value.ValueKind == JsonValueKind.String && FormCatalogValueText.TryParseDate(value.GetString()!, out var date):
                return formatter.FormatDate(date, culture);
            case FormCatalogFieldType.Catalog when OptionOf(value) is { } optionId:
                return optionTexts.GetValueOrDefault(optionId);
            default:
                return null;
        }
    }

    /// <summary>
    /// El texto de las opciones de la fuente vieja que nombran los valores, también las dadas de baja: lo que queda al pasar a texto.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, string>> OptionTextsAsync(FormCatalogFieldChange change, IEnumerable<JsonElement?> stored,
        CancellationToken ct)
    {
        if (!change.Converts || change.Next.Type != FormCatalogFieldType.Text || change.Previous.SourceCatalogId is not { } sourceId)
            return new Dictionary<Guid, string>();
        Guid[] ids = [.. stored.OfType<JsonElement>().Select(OptionOf).OfType<Guid>().Distinct()];
        if (ids.Length == 0) return new Dictionary<Guid, string>();
        return (await reader.ListItemsByIdsAsync(sourceId, ids, ct)).ToDictionary(option => option.Id, option => option.Text);
    }

    /// <summary>Al pasar a «De un catálogo» o cambiar la fuente, cada valor tiene que ser una opción activa de la fuente nueva.</summary>
    private async Task RejectInactiveOptionsAsync(FormCatalogFieldChange change, FormCatalogFieldValue[] converted, CancellationToken ct)
    {
        if (!change.Converts || change.Next.Type != FormCatalogFieldType.Catalog) return;
        var pending = new List<int>();
        var options = new List<FormCatalogFieldOptionValue>();
        for (var index = 0; index < converted.Length; index++)
        {
            if (converted[index].Canonical is not { } canonical || OptionOf(canonical) is not { } optionId) continue;
            pending.Add(index);
            options.Add(new FormCatalogFieldOptionValue(change.Next, optionId, StoredOptionId: null));
        }

        if (options.Count == 0) return;
        var accepted = await values.CheckOptionsAsync(options, ct);
        for (var position = 0; position < pending.Count; position++)
            if (!accepted[position]) converted[pending[position]] = FormCatalogFieldValue.Failed(FormCatalogFieldValueIssue.Invalid);
    }

    /// <summary>
    /// El primer error del campo, en este orden: lo que no convierte o no cumple el límite, las opciones activas sin valor y lo
    /// repetido, primero entre las guardadas y después en una opción del pedido que repite una guardada.
    /// </summary>
    private Error? Decide(FieldReview review, RequestedItems request, DisplayFormatContext culture)
    {
        var (change, index) = (review.Change, review.Change.Index);
        if (review.InvalidCount > 0)
        {
            return change.Converts
                ? FormCatalogErrors.FieldTypeNotConvertible(index, TypeMessage(review.InvalidCount, change.Next.Type, culture), review.InvalidCount, review.InvalidRows)
                : FormCatalogErrors.FieldValuesIncompatible(index, CountMessage(FormCatalogErrors.FieldValuesIncompatibleCode, review.InvalidCount, culture),
                    review.InvalidCount, review.InvalidRows);
        }

        if (review.Missing > 0)
            return FormCatalogErrors.FieldValuesMissing(index, CountMessage(FormCatalogErrors.FieldValuesMissingCode, review.Missing, culture));
        if (!change.ChecksUnique) return null;
        if (review.RepeatedCount > 0)
            return FormCatalogErrors.FieldValuesIncompatible(index, CountMessage(FormCatalogErrors.FieldValuesIncompatibleCode, review.RepeatedCount, culture),
                review.RepeatedCount, review.RepeatedRows);

        foreach (var (itemIndex, input) in request.Sending(change.Next.Id))
        {
            if (input.IsActive is not true || change.Next.Check(input.Fields![change.Next.Id] ?? default).Canonical is not { } canonical
                || FormCatalogFieldEditor.UniqueKeyOf(canonical) is not { } key || !review.Holders.TryGetValue(key, out var holder))
                continue;
            return values.DuplicateErrorOf(itemIndex, change.Next, holder.Position + 1, culture);
        }

        return null;
    }

    /// <summary>Deja los valores convertidos en las opciones que cambian, cargándolas en lotes; las demás no se tocan ni se auditan.</summary>
    private async Task WriteAsync(Guid catalogId, IReadOnlyList<FieldReview> reviews, CancellationToken ct)
    {
        var byItem = new Dictionary<Guid, Dictionary<string, JsonElement?>>();
        foreach (var review in reviews)
            foreach (var (itemId, value) in review.Converted)
            {
                if (!byItem.TryGetValue(itemId, out var converted)) byItem[itemId] = converted = new Dictionary<string, JsonElement?>(StringComparer.Ordinal);
                converted[review.Change.Next.Key] = value;
            }

        foreach (var ids in byItem.Keys.Chunk(ItemsPerBatch))
            foreach (var item in await repository.ListItemsAsync(catalogId, ids, ct))
                item.ReplaceFieldValues(byItem[item.Id]);
    }

    private string CountMessage(string code, int count, DisplayFormatContext culture) =>
        FormsTexts.Get(count == 1 ? code + ".Singular" : code, culture.Profile)
            .Replace("{count}", formatter.FormatDecimal(count, 0, culture), StringComparison.Ordinal);

    /// <summary>«{count} opciones tienen un valor que no es {type}.», con el tipo nuevo dicho en palabras.</summary>
    private string TypeMessage(int count, FormCatalogFieldType type, DisplayFormatContext culture) =>
        CountMessage(FormCatalogErrors.FieldTypeNotConvertibleCode, count, culture).Replace("{type}",
            FormsTexts.Get(FormCatalogErrors.FieldTypeNotConvertibleCode + ".Type." + type, culture.Profile), StringComparison.Ordinal);

    private static string ReasonOf(FormCatalogFieldValueIssue issue) => issue switch
    {
        FormCatalogFieldValueIssue.Length => FormCatalogInvalidRow.Length,
        FormCatalogFieldValueIssue.Range => FormCatalogInvalidRow.Range,
        _ => FormCatalogInvalidRow.Invalid,
    };

    private static Guid? OptionOf(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id) && id != Guid.Empty ? id : null;

    /// <summary>Sin valor para «Obligatorio»: ninguno, o un texto vacío, como en <see cref="FormCatalogItem.SetFieldValues"/>.</summary>
    private static bool IsEmpty(JsonElement? value) =>
        value is not { } present || (present.ValueKind == JsonValueKind.String && present.GetString()!.Length == 0);

    /// <summary>Dos valores iguales por lo que dicen, no por cómo se escribió su JSON.</summary>
    private static bool SameValue(JsonElement? before, JsonElement? after) => (before, after) switch
    {
        (null, null) => true,
        ({ } left, { } right) when left.ValueKind == right.ValueKind => left.ValueKind switch
        {
            JsonValueKind.String => string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal),
            JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => true,
            _ => string.Equals(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal),
        },
        _ => false,
    };

    /// <summary>Lo que el mismo guardado hace con las opciones: las que borra, las cambiadas por su id y las nuevas, con su índice.</summary>
    private sealed class RequestedItems
    {
        private readonly HashSet<Guid> _deleted;
        private readonly Dictionary<Guid, FormCatalogItemInput> _edited = [];
        private readonly List<(int Index, FormCatalogItemInput Input)> _all = [];

        public RequestedItems(FormCatalogItemChanges changes)
        {
            _deleted = [.. changes.DeleteItems];
            for (var index = 0; index < changes.Items.Count; index++)
            {
                var input = changes.Items[index];
                _all.Add((index, input));
                if (input.ItemId is { } itemId) _edited[itemId] = input;
            }
        }

        public bool Deletes(Guid itemId) => _deleted.Contains(itemId);

        /// <summary>Si el pedido trae el valor del campo para esa opción, ya en el tipo nuevo.</summary>
        public bool SendsValue(Guid itemId, Guid fieldId) => _edited.TryGetValue(itemId, out var input) && input.Fields?.ContainsKey(fieldId) == true;

        /// <summary>El estado con que queda: el del pedido si la cambia, y si no, el guardado.</summary>
        public bool IsActive(FormCatalogStoredItemRow row) => _edited.TryGetValue(row.Id, out var input) ? input.IsActive is true : row.IsActive;

        /// <summary>Las opciones del pedido, cambiadas y nuevas, que traen un valor del campo, en el orden del pedido.</summary>
        public IEnumerable<(int Index, FormCatalogItemInput Input)> Sending(Guid fieldId) =>
            _all.Where(entry => entry.Input.Fields?.ContainsKey(fieldId) == true);
    }

    /// <summary>Lo que va juntando la lectura en lotes para un campo que cambió.</summary>
    private sealed class FieldReview(FormCatalogFieldChange change)
    {
        private readonly List<FormCatalogInvalidRow> _invalidRows = [];
        private readonly List<FormCatalogInvalidRow> _repeatedRows = [];

        public FormCatalogFieldChange Change { get; } = change;
        public int InvalidCount { get; private set; }
        public IReadOnlyList<FormCatalogInvalidRow> InvalidRows => _invalidRows;
        public int Missing { get; set; }
        public int RepeatedCount { get; private set; }
        public IReadOnlyList<FormCatalogInvalidRow> RepeatedRows => _repeatedRows;
        /// <summary>La primera opción activa con cada valor, en orden de posición, para «Único».</summary>
        public Dictionary<string, FormCatalogStoredItemRow> Holders { get; } = new(StringComparer.Ordinal);
        /// <summary>El valor convertido de cada opción que cambia; <c>null</c> lo quita.</summary>
        public Dictionary<Guid, JsonElement?> Converted { get; } = [];

        public void Reject(FormCatalogStoredItemRow row, string reason)
        {
            InvalidCount++;
            if (_invalidRows.Count < FormCatalogLimits.MaxInvalidRows) _invalidRows.Add(new FormCatalogInvalidRow(row.Position, row.Id, row.Text, reason));
        }

        public void Hold(string key, FormCatalogStoredItemRow row)
        {
            if (Holders.TryAdd(key, row)) return;
            RepeatedCount++;
            if (_repeatedRows.Count < FormCatalogLimits.MaxInvalidRows)
                _repeatedRows.Add(new FormCatalogInvalidRow(row.Position, row.Id, row.Text, FormCatalogInvalidRow.Invalid));
        }
    }
}

/// <summary>
/// Un campo guardado que sigue activo y cambió en este guardado (E13): su tipo o su fuente (<see cref="Converts"/>, que convierte los
/// valores), su límite, o pasó a obligatorio o a único. <see cref="Index"/> es el del campo en el pedido (<c>fields.{i}</c>).
/// </summary>
internal sealed record FormCatalogFieldChange(int Index, FormCatalogField Previous, FormCatalogField Next)
{
    public bool Converts => Previous.Type != Next.Type || Previous.SourceCatalogId != Next.SourceCatalogId;
    public bool ChecksLimits => Converts || Previous.MaxLength != Next.MaxLength || !string.Equals(Previous.Min, Next.Min, StringComparison.Ordinal)
        || !string.Equals(Previous.Max, Next.Max, StringComparison.Ordinal);
    public bool ChecksRequired => Next.Required && (Converts || !Previous.Required);
    public bool ChecksUnique => Next.Unique && (Converts || !Previous.Unique);
    public bool Matters => ChecksLimits || ChecksRequired || ChecksUnique;

    public override string ToString() => nameof(FormCatalogFieldChange);
}
