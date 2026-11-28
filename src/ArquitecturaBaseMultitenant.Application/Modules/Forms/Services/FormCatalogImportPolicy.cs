using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Domain.Forms;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;

/// <summary>
/// Decide, sin escribir, qué hace cada fila de lo leído contra lo guardado (C7). La identidad de una fila es su código, sin
/// distinguir mayúsculas: con uno guardado es un cambio, y sin él, un alta al final. Una columna que no vino conserva lo guardado,
/// y presente y vacía, limpia; un dato obligatorio que falta, un texto largo, un valor ambiguo o inválido y un código repetido son
/// errores de su fila y columna. Al reemplazar, lo activo que no vino se da de baja, salvo que su código venga en una fila con
/// errores. Las reglas de cada dato son las del dominio, que valida la fila entera con <see cref="FormCatalogItem.Create"/>.
/// Desde la E13, cada campo activo cuya columna vino suma su valor, con la regla de <see cref="FormCatalogField.Check"/>: vacía limpia,
/// un valor que no sirve es <see cref="FormCatalogErrors.FieldValueInvalidCode"/> de su columna, falta uno obligatorio en una
/// opción activa (<see cref="FormCatalogErrors.FieldValueRequiredCode"/>) o un valor único lo tiene otra opción activa, del archivo o
/// de las que quedan (<see cref="FormCatalogErrors.FieldValueDuplicateCode"/>). La columna de un campo es su nombre.
/// </summary>
internal static class FormCatalogImportPolicy
{
    /// <summary>Más del 25 % de bajas pide confirmar la cantidad exacta, y más del 10 % de filas con error, confirmarlo (C7).</summary>
    internal const decimal DeactivationShareToConfirm = 0.25m;
    internal const decimal ErrorShareToConfirm = 0.10m;

    public static FormCatalogImportPlan Decide(FormCatalogValueType valueType, FormCatalogImportMode mode, FormCatalogFile file,
        IReadOnlyList<FormCatalogStoredItemRow> saved, int activeItems, FormCatalogImportFields? fields = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(saved);
        fields ??= FormCatalogImportFields.None;
        var byCode = saved.ToDictionary(item => item.CodeNormalized, StringComparer.Ordinal);
        var probe = FormCatalog.CreateManual("-", null, valueType);
        var firstRows = new Dictionary<string, int>(StringComparer.Ordinal);
        var rows = new List<RowDecision>(file.Rows.Count);

        foreach (var row in file.Rows)
        {
            var decision = new RowDecision(row.Row);
            if (row.Code is null) decision.Fail(FormCatalogFileResolver.CodeColumn, FormCatalogImportErrors.CellRequiredCode);
            else if (row.Code.Length > FormsTextLimits.OptionCode) decision.Fail(FormCatalogFileResolver.CodeColumn, FormCatalogErrors.ItemTextTooLongCode);
            else
            {
                var normalized = FormCatalogItem.NormalizeCode(row.Code);
                if (firstRows.TryGetValue(normalized, out var first))
                    decision.Fail(FormCatalogFileResolver.CodeColumn, FormCatalogErrors.DuplicateCodeCode, otherRow: first);
                else firstRows[normalized] = row.Row;
                decision.Existing = byCode.GetValueOrDefault(normalized);
            }

            var existing = decision.Existing;
            var text = file.HasText ? row.Text : existing?.Text;
            if (text is null) decision.Fail(FormCatalogFileResolver.TextColumn, FormCatalogImportErrors.CellRequiredCode);
            else if (text?.Length > FormsTextLimits.OptionText) decision.Fail(FormCatalogFileResolver.TextColumn, FormCatalogErrors.ItemTextTooLongCode);
            var detail = file.HasDetail ? row.Detail : existing?.Detail;
            if (detail?.Length > FormsTextLimits.OptionDetail) decision.Fail(FormCatalogFileResolver.DetailColumn, FormCatalogErrors.ItemDetailTooLongCode);
            var value = valueType == FormCatalogValueType.Text ? null : Value(file, row, existing, decision);
            ReadFields(fields, row, decision);
            CheckRequired(fields, decision);

            if (decision.Errors.Count == 0 && row.Code is not null && text is not null)
            {
                var item = FormCatalogItem.Create(probe, 0, row.Code, text, detail, value, isActive: true, position: 0);
                if (item.IsFailure) decision.Fail(DomainColumn(item.Error.Code), DomainCode(item.Error.Code));
                else decision.Item = item.Value;
            }
            rows.Add(decision);
        }

        // Una fila con errores también protege su código de la baja: no aplicarla no la borra.
        var present = new HashSet<string>(file.Rows.Where(row => row.Code is { Length: <= FormsTextLimits.OptionCode })
            .Select(row => FormCatalogItem.NormalizeCode(row.Code!)), StringComparer.Ordinal);
        CheckUnique(fields, mode, saved, rows, present);
        return Plan(mode, fields, saved, rows, present, activeItems);
    }

    /// <summary>Las listas, las escrituras y las bajas de las filas ya decididas, en el orden del archivo.</summary>
    private static FormCatalogImportPlan Plan(FormCatalogImportMode mode, FormCatalogImportFields fields,
        IReadOnlyList<FormCatalogStoredItemRow> saved, IReadOnlyList<RowDecision> rows, HashSet<string> present, int activeItems)
    {
        var errors = new List<FormCatalogImportErrorRow>();
        var added = new List<FormCatalogImportAddedRow>();
        var changed = new List<FormCatalogImportChangedRow>();
        var writes = new List<FormCatalogImportWrite>();
        var (unchanged, errorRows) = (0, 0);
        foreach (var decision in rows)
        {
            if (decision.Errors.Count > 0)
            {
                errorRows++;
                errors.AddRange(decision.Errors);
                continue;
            }

            var item = decision.Item!;
            if (decision.Existing is not { } existing)
            {
                added.Add(new FormCatalogImportAddedRow(decision.Row, item.Code, item.Text));
                writes.Add(new FormCatalogImportWrite(decision.Row, null, item.Code, item.Text, item.Detail, item.ValueText(), decision.Delta));
                continue;
            }

            var differences = Differences(decision, existing, item, fields);
            if (differences.Count == 0) unchanged++;
            else
            {
                changed.AddRange(differences);
                writes.Add(new FormCatalogImportWrite(decision.Row, existing.Id, item.Code, item.Text, item.Detail, item.ValueText(),
                    decision.Delta));
            }
        }

        var deactivated = mode == FormCatalogImportMode.Replace
            ? saved.Where(item => item.IsActive && !present.Contains(item.CodeNormalized)).OrderBy(item => item.Position).ToList()
            : [];
        var lists = new FormCatalogImportLists(errors, added, changed,
            [.. deactivated.Select(item => new FormCatalogImportDeactivatedRow(item.Code, item.Text))]);
        return new FormCatalogImportPlan(rows.Count, unchanged, errorRows, lists, writes, [.. deactivated.Select(item => item.Id)],
            Share(deactivated.Count, activeItems), Share(errorRows, rows.Count));
    }

    private static string? Value(FormCatalogFile file, FormCatalogFileRow row, FormCatalogStoredItemRow? existing, RowDecision decision)
    {
        if (!file.HasValue || row.Value is null)
        {
            if (existing is null) decision.Fail(FormCatalogFileResolver.ValueColumn, FormCatalogImportErrors.CellRequiredCode);
            return existing?.Value;
        }
        if (row.Value.IsEmpty) decision.Fail(FormCatalogFileResolver.ValueColumn, FormCatalogImportErrors.CellRequiredCode);
        else if (row.Value.ErrorCode is { } code)
            decision.Fail(FormCatalogFileResolver.ValueColumn, code, code == FormCatalogImportErrors.ValueAmbiguousCode ? row.Value.Raw : null);
        return row.Value.Canonical;
    }

    /// <summary>
    /// El valor de cada campo cuya columna vino: el leído, o el de una opción de su fuente, donde la guardada se conserva aunque esté
    /// dada de baja (lección 1). Solo lo que cambia va al delta: vacía limpia lo que había, y un texto vacío guardado es como nada.
    /// </summary>
    private static void ReadFields(FormCatalogImportFields fields, FormCatalogFileRow row, RowDecision decision)
    {
        if (row.Fields is null || row.Fields.Count == 0) return;
        foreach (var field in fields.Active)
        {
            if (!row.Fields.TryGetValue(field.Id, out var cell)) continue;
            var read = cell.Value ?? fields.Options.Resolve(field, cell.Raw!, StoredOption(decision.Stored, field.Key));
            if (read.Issue is { } issue)
            {
                var ambiguous = issue == FormCatalogFieldValueIssue.Ambiguous;
                decision.FailField(field, ambiguous ? FormCatalogImportErrors.ValueAmbiguousCode : FormCatalogErrors.FieldValueInvalidCode,
                    ambiguous ? cell.Raw : null);
                continue;
            }

            var had = decision.Stored.TryGetValue(field.Key, out var before) && !IsBlank(before);
            if (read.Canonical is { } canonical)
            {
                if (had && string.Equals(before.GetRawText(), canonical.GetRawText(), StringComparison.Ordinal)) continue;
                decision.Values[field.Key] = canonical;
                decision.Delta[field.Id] = canonical;
            }
            else if (had)
            {
                decision.Values.Remove(field.Key);
                decision.Delta[field.Id] = null;
            }
        }
    }

    /// <summary>Una opción activa necesita un valor en cada campo obligatorio, venga o no su columna.</summary>
    private static void CheckRequired(FormCatalogImportFields fields, RowDecision decision)
    {
        if (!decision.IsActive) return;
        foreach (var field in fields.Active.Where(field => field.Required && !decision.FailedFields.Contains(field.Id)))
            if (!decision.Values.TryGetValue(field.Key, out var value) || IsBlank(value))
                decision.FailField(field, FormCatalogErrors.FieldValueRequiredCode);
    }

    /// <summary>
    /// Un valor único no puede estar en dos opciones activas. Lo tienen las filas que se aplican, en el orden del archivo, y lo que
    /// queda como está: al agregar, las activas que no vinieron, y las de las filas con errores, que no se aplican. Una fila que
    /// repite deja de aplicarse y conserva lo suyo, así que se vuelve a mirar hasta que ninguna cambie.
    /// </summary>
    private static void CheckUnique(FormCatalogImportFields fields, FormCatalogImportMode mode, IReadOnlyList<FormCatalogStoredItemRow> saved,
        IReadOnlyList<RowDecision> rows, HashSet<string> present)
    {
        var unique = fields.Active.Where(field => field.Unique).ToList();
        if (unique.Count == 0) return;
        var kept = mode == FormCatalogImportMode.Append
            ? saved.Where(item => item.IsActive && !present.Contains(item.CodeNormalized))
                .Select(item => FormCatalogItem.FieldValuesFromJson(item.FieldValuesJson)).ToList()
            : [];
        bool repeated;
        do
        {
            repeated = false;
            foreach (var field in unique)
            {
                var applied = rows.Where(row => row.Errors.Count == 0 && row.Existing is not null).Select(row => row.Existing!.Id).ToHashSet();
                var held = new Dictionary<string, int?>(StringComparer.Ordinal);
                foreach (var values in kept)
                    if (UniqueKey(values, field) is { } key) held.TryAdd(key, null);
                foreach (var row in rows.Where(row => row.Errors.Count > 0 && row.Existing is { IsActive: true } && !applied.Contains(row.Existing.Id)))
                    if (UniqueKey(row.Stored, field) is { } key) held.TryAdd(key, row.Row);
                foreach (var row in rows.Where(row => row.Errors.Count == 0 && row.IsActive))
                {
                    if (UniqueKey(row.Values, field) is not { } key) continue;
                    if (held.TryGetValue(key, out var other))
                    {
                        row.FailField(field, FormCatalogErrors.FieldValueDuplicateCode, otherRow: other);
                        repeated = true;
                    }
                    else held[key] = row.Row;
                }
            }
        }
        while (repeated);
    }

    /// <summary>Lo que cambió, columna por columna y en el orden de la plantilla, con el antes y el después.</summary>
    private static List<FormCatalogImportChangedRow> Differences(RowDecision decision, FormCatalogStoredItemRow saved, FormCatalogItem next,
        FormCatalogImportFields fields)
    {
        var differences = new List<FormCatalogImportChangedRow>();
        void Compare(string column, string? before, string? after)
        {
            if (!string.Equals(before, after, StringComparison.Ordinal))
                differences.Add(new FormCatalogImportChangedRow(decision.Row, next.Code, column, before, after));
        }

        Compare(FormCatalogFileResolver.CodeColumn, saved.Code, next.Code);
        Compare(FormCatalogFileResolver.TextColumn, saved.Text, next.Text);
        Compare(FormCatalogFileResolver.ValueColumn, saved.Value, next.ValueText());
        // Un campo que cambió dice el antes y el después como se leen: el número y la fecha en la cultura, «Sí» o «No», la opción.
        foreach (var field in fields.Active)
        {
            if (!decision.Delta.TryGetValue(field.Id, out var after)) continue;
            var before = decision.Stored.TryGetValue(field.Key, out var stored) ? fields.TextOf(field, stored) : null;
            differences.Add(new FormCatalogImportChangedRow(decision.Row, next.Code, field.Name, before,
                after is { } value ? fields.TextOf(field, value) : null));
        }
        Compare(FormCatalogFileResolver.DetailColumn, saved.Detail, next.Detail);
        return differences;
    }

    /// <summary>La opción que tenía guardada ese campo, si es una identidad.</summary>
    private static Guid? StoredOption(IReadOnlyDictionary<string, JsonElement> stored, string key) =>
        stored.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id)
            ? id : null;

    private static string? UniqueKey(IReadOnlyDictionary<string, JsonElement> values, FormCatalogField field) =>
        values.TryGetValue(field.Key, out var value) ? FormCatalogFieldEditor.UniqueKeyOf(value) : null;

    private static bool IsBlank(JsonElement value) => value.ValueKind == JsonValueKind.String && value.GetString()!.Trim().Length == 0;

    private static string DomainColumn(string code) => code switch
    {
        FormCatalogErrors.ItemTextTooLongCode => FormCatalogFileResolver.TextColumn,
        FormCatalogErrors.ItemDetailTooLongCode => FormCatalogFileResolver.DetailColumn,
        _ => FormCatalogFileResolver.ValueColumn,
    };

    private static string DomainCode(string code) =>
        code == FormCatalogErrors.ValueRequiredCode ? FormCatalogImportErrors.CellRequiredCode : code;

    private static decimal Share(int part, int whole) => whole == 0 ? 0 : (decimal)part / whole;

    /// <summary>
    /// Lo que se decide de una fila mientras se lee: sus errores, la opción guardada con su código, la que quedaría y los valores de
    /// sus campos, los que quedan y el delta de los que cambian.
    /// </summary>
    private sealed class RowDecision(int row)
    {
        private static readonly IReadOnlyDictionary<string, JsonElement> NoValues = new Dictionary<string, JsonElement>();
        private FormCatalogStoredItemRow? _existing;

        public int Row { get; } = row;
        public List<FormCatalogImportErrorRow> Errors { get; } = [];
        public HashSet<Guid> FailedFields { get; } = [];
        public FormCatalogItem? Item { get; set; }
        public IReadOnlyDictionary<string, JsonElement> Stored { get; private set; } = NoValues;
        public Dictionary<string, JsonElement> Values { get; private set; } = new(StringComparer.Ordinal);
        public Dictionary<Guid, JsonElement?> Delta { get; } = [];

        /// <summary>La opción guardada con el código de la fila; con ella llegan los valores de sus campos.</summary>
        public FormCatalogStoredItemRow? Existing
        {
            get => _existing;
            set
            {
                _existing = value;
                Stored = value is null ? NoValues : FormCatalogItem.FieldValuesFromJson(value.FieldValuesJson);
                Values = new Dictionary<string, JsonElement>(Stored, StringComparer.Ordinal);
            }
        }

        /// <summary>Una opción nueva nace activa; una guardada conserva su estado.</summary>
        public bool IsActive => _existing?.IsActive ?? true;

        public void Fail(string column, string code, string? value = null, int? otherRow = null) =>
            Errors.Add(new FormCatalogImportErrorRow(Row, column, code, value, otherRow));

        public void FailField(FormCatalogField field, string code, string? value = null, int? otherRow = null)
        {
            FailedFields.Add(field.Id);
            Fail(field.Name, code, value, otherRow);
        }

        public override string ToString() => nameof(RowDecision);
    }
}

/// <summary>
/// Los campos activos de un catálogo a mano para importar (E13): las opciones de las fuentes de los que vinieron y cómo se lee un
/// valor guardado en la cultura del pedido, para el antes y el después de un cambio.
/// </summary>
internal sealed record FormCatalogImportFields(IReadOnlyList<FormCatalogField> Active, FormCatalogFieldOptions Options,
    Func<FormCatalogField, JsonElement, string?> Display)
{
    public static FormCatalogImportFields None { get; } = new([], FormCatalogFieldOptions.None, (_, _) => null);

    /// <summary>Cómo se lee un valor; si no se puede, su forma canónica.</summary>
    public string TextOf(FormCatalogField field, JsonElement value) =>
        Display(field, value) ?? (value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText());

    public override string ToString() => nameof(FormCatalogImportFields);
}

/// <summary>
/// Las opciones de las fuentes de los campos «De un catálogo» (E13), todas, activas y dadas de baja: una celda nombra una opción por
/// su identidad, su código o su texto, si ninguna otra activa lo tiene. La que ya estaba guardada se conserva aunque esté dada de
/// baja; una nueva tiene que estar activa (lección 1). Al exportar, una opción va con su texto, o con su código si el texto llevaría
/// a otra: el código de otra opción, o el texto de otra activa. El texto y el código se comparan sin mayúsculas, como el código.
/// </summary>
internal sealed class FormCatalogFieldOptions
{
    private readonly Dictionary<Guid, Source> _sources;

    public FormCatalogFieldOptions(IReadOnlyDictionary<Guid, IReadOnlyList<FormCatalogStoredItemRow>> optionsBySource)
    {
        ArgumentNullException.ThrowIfNull(optionsBySource);
        _sources = optionsBySource.ToDictionary(entry => entry.Key, entry => new Source(entry.Value));
    }

    public static FormCatalogFieldOptions None { get; } = new(new Dictionary<Guid, IReadOnlyList<FormCatalogStoredItemRow>>());

    /// <summary>La opción que nombra <paramref name="raw"/> en la fuente del campo, como valor canónico, o por qué no sirve.</summary>
    /// <param name="field">Un campo «De un catálogo».</param>
    /// <param name="raw">Lo que dice la celda: la identidad, el código o el texto de la opción.</param>
    /// <param name="stored">La opción que tenía guardada la opción en ese campo, si tenía.</param>
    public FormCatalogFieldValue Resolve(FormCatalogField field, string raw, Guid? stored)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(raw);
        var text = raw.Trim();
        if (text.Length == 0) return FormCatalogFieldValue.Empty;
        var source = field.SourceCatalogId is { } sourceId ? _sources.GetValueOrDefault(sourceId) : null;
        var id = Guid.TryParse(text, out var parsed) ? parsed : (Guid?)null;
        if (stored is { } mine && (id == mine || (source?.ById.GetValueOrDefault(mine) is { } option && Source.Names(option, text, id))))
            return Of(field, mine);
        var found = source?.Find(text, id);
        return found is { IsActive: true } ? Of(field, found.Id) : FormCatalogFieldValue.Failed(FormCatalogFieldValueIssue.Invalid);
    }

    /// <summary>El texto de la opción, también dada de baja; <c>null</c> si la fuente no la tiene.</summary>
    public string? TextOf(Guid? sourceId, Guid optionId) =>
        sourceId is { } id && _sources.GetValueOrDefault(id)?.ById.GetValueOrDefault(optionId) is { } option ? option.Text : null;

    /// <summary>
    /// Lo que va en la celda exportada: el texto de la opción o, si ese texto llevaría a otra, su código. Una opción que la fuente ya
    /// no tiene va con su identidad, que se vuelve a importar.
    /// </summary>
    public string ExportText(Guid? sourceId, Guid optionId)
    {
        var source = sourceId is { } id ? _sources.GetValueOrDefault(id) : null;
        if (source?.ById.GetValueOrDefault(optionId) is not { } option) return optionId.ToString("D");
        var found = source.Find(option.Text, Guid.TryParse(option.Text, out var parsed) ? parsed : null);
        return (found is null || found.Id == option.Id) && !source.TextIsShared(option) ? option.Text : option.Code;
    }

    public override string ToString() => nameof(FormCatalogFieldOptions);

    private sealed class Source(IReadOnlyList<FormCatalogStoredItemRow> options)
    {
        public Dictionary<Guid, FormCatalogStoredItemRow> ById { get; } = options.ToDictionary(option => option.Id);
        private Dictionary<string, FormCatalogStoredItemRow> ByCode { get; } =
            options.GroupBy(option => option.CodeNormalized, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        private Dictionary<string, List<FormCatalogStoredItemRow>> ByText { get; } =
            options.GroupBy(option => FormCatalogItem.NormalizeCode(option.Text), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        /// <summary>Si el texto nombra esa opción: su identidad, su código o su texto.</summary>
        public static bool Names(FormCatalogStoredItemRow option, string text, Guid? id)
        {
            if (id == option.Id) return true;
            var key = FormCatalogItem.NormalizeCode(text);
            return key == option.CodeNormalized || key == FormCatalogItem.NormalizeCode(option.Text);
        }

        /// <summary>La opción que nombra el texto: por su identidad, por su código o por un texto que tiene una sola activa.</summary>
        public FormCatalogStoredItemRow? Find(string text, Guid? id)
        {
            if (id is { } optionId && ById.TryGetValue(optionId, out var byId)) return byId;
            var key = FormCatalogItem.NormalizeCode(text);
            if (ByCode.TryGetValue(key, out var byCode)) return byCode;
            var active = ByText.GetValueOrDefault(key)?.Where(option => option.IsActive).Take(2).ToList();
            return active is [var single] ? single : null;
        }

        /// <summary>Si otra opción activa tiene el mismo texto.</summary>
        public bool TextIsShared(FormCatalogStoredItemRow option) =>
            ByText.GetValueOrDefault(FormCatalogItem.NormalizeCode(option.Text))?.Any(other => other.Id != option.Id && other.IsActive) == true;
    }

    private static FormCatalogFieldValue Of(FormCatalogField field, Guid optionId) =>
        field.Check(JsonSerializer.SerializeToElement(optionId.ToString("D")));
}

/// <summary>
/// Lo que haría la importación: las cuatro listas de la revisión, lo que se escribe al aplicar y las proporciones, sin redondear,
/// con que se decide qué confirmar.
/// </summary>
internal sealed record FormCatalogImportPlan(int RowsRead, int Unchanged, int ErrorRows, FormCatalogImportLists Lists,
    IReadOnlyList<FormCatalogImportWrite> Writes, IReadOnlyList<Guid> Deactivations, decimal DeactivationShare, decimal ErrorShare)
{
    public bool ConfirmDeactivations => DeactivationShare > FormCatalogImportPolicy.DeactivationShareToConfirm;
    public bool ConfirmErrors => ErrorShare > FormCatalogImportPolicy.ErrorShareToConfirm;

    /// <summary>La revisión, con las cuatro listas en la misma página y las proporciones como fracción de cuatro decimales.</summary>
    public FormCatalogImportPreviewResponse ToPreview(uint version, string sha256, string? sheetName, int page, int pageSize) =>
        new(version, sha256, sheetName, RowsRead, Unchanged, Math.Round(DeactivationShare, 4, MidpointRounding.AwayFromZero),
            Math.Round(ErrorShare, 4, MidpointRounding.AwayFromZero), ConfirmDeactivations, ConfirmErrors,
            Page(Lists.Errors, page, pageSize), Page(Lists.Added, page, pageSize), Page(Lists.Changed, page, pageSize),
            Page(Lists.Deactivated, page, pageSize));

    private static PagedResult<T> Page<T>(IReadOnlyList<T> items, int page, int pageSize) =>
        new([.. items.Skip((page - 1) * pageSize).Take(pageSize)], page, pageSize, items.Count);

    public override string ToString() => nameof(FormCatalogImportPlan);
}

/// <summary>Las cuatro listas de la revisión, enteras: los errores por fila y columna, las altas, los cambios y las bajas.</summary>
internal sealed record FormCatalogImportLists(IReadOnlyList<FormCatalogImportErrorRow> Errors, IReadOnlyList<FormCatalogImportAddedRow> Added,
    IReadOnlyList<FormCatalogImportChangedRow> Changed, IReadOnlyList<FormCatalogImportDeactivatedRow> Deactivated)
{
    public override string ToString() => nameof(FormCatalogImportLists);
}

/// <summary>
/// Una fila que se escribe al aplicar: un cambio de la opción guardada o, sin id, un alta al final, con el delta de los valores de sus
/// campos por la identidad del campo (E13).
/// </summary>
internal sealed record FormCatalogImportWrite(int Row, Guid? ItemId, string Code, string Text, string? Detail, string? Value,
    IReadOnlyDictionary<Guid, JsonElement?> Fields)
{
    public override string ToString() => nameof(FormCatalogImportWrite);
}
