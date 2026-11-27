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
/// </summary>
internal static class FormCatalogImportPolicy
{
    /// <summary>Más del 25 % de bajas pide confirmar la cantidad exacta, y más del 10 % de filas con error, confirmarlo (C7).</summary>
    internal const decimal DeactivationShareToConfirm = 0.25m;
    internal const decimal ErrorShareToConfirm = 0.10m;

    public static FormCatalogImportPlan Decide(FormCatalogValueType valueType, FormCatalogImportMode mode, FormCatalogFile file,
        IReadOnlyList<FormCatalogStoredItemRow> saved, int activeItems)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(saved);
        var byCode = saved.ToDictionary(item => item.CodeNormalized, StringComparer.Ordinal);
        var probe = FormCatalog.CreateManual("-", null, valueType);
        var firstRows = new Dictionary<string, int>(StringComparer.Ordinal);
        var errors = new List<FormCatalogImportErrorRow>();
        var added = new List<FormCatalogImportAddedRow>();
        var changed = new List<FormCatalogImportChangedRow>();
        var writes = new List<FormCatalogImportWrite>();
        var (rowsRead, unchanged, errorRows) = (0, 0, 0);

        foreach (var row in file.Rows)
        {
            rowsRead++;
            var rowErrors = new List<FormCatalogImportErrorRow>();
            void Fail(string column, string code, string? value = null, int? otherRow = null) =>
                rowErrors.Add(new FormCatalogImportErrorRow(row.Row, column, code, value, otherRow));

            FormCatalogStoredItemRow? existing = null;
            if (row.Code is null) Fail(FormCatalogFileResolver.CodeColumn, FormCatalogImportErrors.CellRequiredCode);
            else if (row.Code.Length > FormsTextLimits.OptionCode) Fail(FormCatalogFileResolver.CodeColumn, FormCatalogErrors.ItemTextTooLongCode);
            else
            {
                var normalized = FormCatalogItem.NormalizeCode(row.Code);
                if (firstRows.TryGetValue(normalized, out var first))
                    Fail(FormCatalogFileResolver.CodeColumn, FormCatalogErrors.DuplicateCodeCode, otherRow: first);
                else firstRows[normalized] = row.Row;
                existing = byCode.GetValueOrDefault(normalized);
            }

            var text = file.HasText ? row.Text : existing?.Text;
            if (text is null) Fail(FormCatalogFileResolver.TextColumn, FormCatalogImportErrors.CellRequiredCode);
            else if (text?.Length > FormsTextLimits.OptionText) Fail(FormCatalogFileResolver.TextColumn, FormCatalogErrors.ItemTextTooLongCode);
            var detail = file.HasDetail ? row.Detail : existing?.Detail;
            if (detail?.Length > FormsTextLimits.OptionDetail) Fail(FormCatalogFileResolver.DetailColumn, FormCatalogErrors.ItemDetailTooLongCode);
            var value = valueType == FormCatalogValueType.Text ? null : Value(file, row, existing, Fail);

            if (rowErrors.Count == 0 && row.Code is not null && text is not null)
            {
                var item = FormCatalogItem.Create(probe, 0, row.Code, text, detail, value, isActive: true, position: 0);
                if (item.IsFailure) Fail(DomainColumn(item.Error.Code), DomainCode(item.Error.Code));
                else if (existing is null)
                {
                    added.Add(new FormCatalogImportAddedRow(row.Row, item.Value.Code, item.Value.Text));
                    writes.Add(new FormCatalogImportWrite(row.Row, null, item.Value.Code, item.Value.Text, item.Value.Detail, item.Value.ValueText()));
                }
                else
                {
                    var differences = Differences(row.Row, existing, item.Value);
                    if (differences.Count == 0) unchanged++;
                    else
                    {
                        changed.AddRange(differences);
                        writes.Add(new FormCatalogImportWrite(row.Row, existing.Id, item.Value.Code, item.Value.Text, item.Value.Detail,
                            item.Value.ValueText()));
                    }
                }
            }

            if (rowErrors.Count == 0) continue;
            errorRows++;
            errors.AddRange(rowErrors);
        }

        // Una fila con errores también protege su código de la baja: no aplicarla no la borra.
        var present = new HashSet<string>(file.Rows.Where(row => row.Code is { Length: <= FormsTextLimits.OptionCode })
            .Select(row => FormCatalogItem.NormalizeCode(row.Code!)), StringComparer.Ordinal);
        var deactivated = mode == FormCatalogImportMode.Replace
            ? saved.Where(item => item.IsActive && !present.Contains(item.CodeNormalized)).OrderBy(item => item.Position).ToList()
            : [];
        var lists = new FormCatalogImportLists(errors, added, changed,
            [.. deactivated.Select(item => new FormCatalogImportDeactivatedRow(item.Code, item.Text))]);
        return new FormCatalogImportPlan(rowsRead, unchanged, errorRows, lists, writes, [.. deactivated.Select(item => item.Id)],
            Share(deactivated.Count, activeItems), Share(errorRows, rowsRead));
    }

    private static string? Value(FormCatalogFile file, FormCatalogFileRow row, FormCatalogStoredItemRow? existing,
        Action<string, string, string?, int?> fail)
    {
        if (!file.HasValue || row.Value is null)
        {
            if (existing is null) fail(FormCatalogFileResolver.ValueColumn, FormCatalogImportErrors.CellRequiredCode, null, null);
            return existing?.Value;
        }
        if (row.Value.IsEmpty) fail(FormCatalogFileResolver.ValueColumn, FormCatalogImportErrors.CellRequiredCode, null, null);
        else if (row.Value.ErrorCode is { } code)
            fail(FormCatalogFileResolver.ValueColumn, code, code == FormCatalogImportErrors.ValueAmbiguousCode ? row.Value.Raw : null, null);
        return row.Value.Canonical;
    }

    /// <summary>Lo que cambió, columna por columna y en el orden de la plantilla, con el antes y el después.</summary>
    private static List<FormCatalogImportChangedRow> Differences(int row, FormCatalogStoredItemRow saved, FormCatalogItem next)
    {
        var differences = new List<FormCatalogImportChangedRow>();
        void Compare(string column, string? before, string? after)
        {
            if (!string.Equals(before, after, StringComparison.Ordinal))
                differences.Add(new FormCatalogImportChangedRow(row, next.Code, column, before, after));
        }

        Compare(FormCatalogFileResolver.CodeColumn, saved.Code, next.Code);
        Compare(FormCatalogFileResolver.TextColumn, saved.Text, next.Text);
        Compare(FormCatalogFileResolver.ValueColumn, saved.Value, next.ValueText());
        Compare(FormCatalogFileResolver.DetailColumn, saved.Detail, next.Detail);
        return differences;
    }

    private static string DomainColumn(string code) => code switch
    {
        FormCatalogErrors.ItemTextTooLongCode => FormCatalogFileResolver.TextColumn,
        FormCatalogErrors.ItemDetailTooLongCode => FormCatalogFileResolver.DetailColumn,
        _ => FormCatalogFileResolver.ValueColumn,
    };

    private static string DomainCode(string code) =>
        code == FormCatalogErrors.ValueRequiredCode ? FormCatalogImportErrors.CellRequiredCode : code;

    private static decimal Share(int part, int whole) => whole == 0 ? 0 : (decimal)part / whole;
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

/// <summary>Una fila que se escribe al aplicar: un cambio de la opción guardada o, sin id, un alta al final.</summary>
internal sealed record FormCatalogImportWrite(int Row, Guid? ItemId, string Code, string Text, string? Detail, string? Value)
{
    public override string ToString() => nameof(FormCatalogImportWrite);
}
