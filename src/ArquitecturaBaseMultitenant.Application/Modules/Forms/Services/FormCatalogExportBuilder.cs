using System.Globalization;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Resources;
using ArquitecturaBaseMultitenant.Domain.Forms;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;

/// <summary>
/// Arma la plantilla y las filas que se exportan (C10), con las columnas de la plantilla en la cultura del pedido: Código, Texto,
/// «Valor (tipo)» en un catálogo tipado y Detalle. Entero y decimal van como números de Excel, con separador de miles y tantos
/// decimales como tenga el valor, y la fecha, como fecha de Excel con el formato del idioma. Un número de más de 15 cifras
/// significativas, que una celda numérica no guarda, y una fecha que Excel no cuenta bien (antes del 1 de marzo de 1900) van como
/// su texto canónico, que se reimporta sin ambigüedad. Desde la E13, una columna por campo activo, con su nombre, antes del detalle
/// (119): número y fecha como el valor, sí/no con su palabra y «De un catálogo» con el texto de la opción o, si ese texto llevaría a
/// otra, con su código; así se vuelve a importar sin diferencias.
/// </summary>
internal static class FormCatalogExportBuilder
{
    public const string MediaType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const int MaxSignificantDigits = 15;
    private static readonly DateOnly FirstExcelDate = new(1900, 3, 1);

    /// <summary>
    /// Los encabezados, con los de los campos activos por su nombre (E13); la plantilla dice en la nota de cada uno si es
    /// obligatorio. La plantilla y la exportación nombran igual las columnas (O14).
    /// </summary>
    public static FormCatalogSheetLayout Layout(FormCatalogValueType valueType, bool withNotes, IReadOnlyList<FormCatalogField>? fields = null)
    {
        string? Note(bool required) => withNotes ? FormsTexts.Get(required ? "CatalogImport.Required" : "CatalogImport.Optional") : null;
        FormCatalogSheetHeader Header(string key, bool required) => new(FormsTexts.Get(key), Note(required));

        var headers = new List<FormCatalogSheetHeader>
        {
            Header("CatalogImport.Header.Code", required: true),
            Header("CatalogImport.Header.Text", required: true),
        };
        if (valueType != FormCatalogValueType.Text) headers.Add(Header("CatalogImport.Header.Value." + valueType, required: true));
        headers.AddRange((fields ?? []).Select(field => new FormCatalogSheetHeader(field.Name, Note(field.Required))));
        headers.Add(Header("CatalogImport.Header.Detail", required: false));
        return new FormCatalogSheetLayout(FormsTexts.Get("CatalogImport.SheetName"), headers);
    }

    public static IReadOnlyList<FormCatalogCell> Row(FormCatalogStoredItemRow item, FormCatalogValueType valueType, CultureProfile profile,
        FormCatalogExportFields? fields = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(profile);
        IReadOnlyList<FormCatalogCell> fieldCells = [];
        if (fields is { Fields.Count: > 0 })
        {
            var values = FormCatalogItem.FieldValuesFromJson(item.FieldValuesJson);
            fieldCells = [.. fields.Fields.Select(field => FieldCell(field, values, fields, profile))];
        }
        return Cells(item.Code, item.Text, item.Value, item.Detail, valueType, profile, fieldCells);
    }

    /// <summary>La fila de ejemplo de la plantilla, con un valor del tipo y las celdas de los campos vacías.</summary>
    public static IReadOnlyList<FormCatalogCell> Example(FormCatalogValueType valueType, CultureProfile profile, int fieldCount = 0)
    {
        var value = valueType switch
        {
            FormCatalogValueType.WholeNumber => "1500",
            FormCatalogValueType.DecimalNumber => "2500.5",
            FormCatalogValueType.Date => "2026-03-31",
            _ => null,
        };
        return Cells(FormsTexts.Get("CatalogImport.Example.Code"), FormsTexts.Get("CatalogImport.Example.Text"), value,
            FormsTexts.Get("CatalogImport.Example.Detail"), valueType, profile, [.. Enumerable.Repeat(FormCatalogCell.Blank, fieldCount)]);
    }

    /// <summary>El nombre del archivo: el del catálogo, sin los caracteres que un sistema de archivos no acepta.</summary>
    public static string FileName(string catalogName)
    {
        ArgumentNullException.ThrowIfNull(catalogName);
        var safe = new string([.. catalogName.Select(character => char.IsControl(character) || "\\/:*?\"<>|".Contains(character) ? '_' : character)]);
        return safe.Trim() + ".xlsx";
    }

    internal static FormCatalogCell ValueCell(string? canonical, FormCatalogValueType valueType, CultureProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (canonical is null) return FormCatalogCell.Blank;
        if (valueType == FormCatalogValueType.Date)
        {
            var date = DateOnly.ParseExact(canonical, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            return date < FirstExcelDate ? new FormCatalogCell(canonical)
                : new FormCatalogCell(null, Date: date, Format: profile.Entry.DatePattern.ToLowerInvariant());
        }

        var digits = canonical.TrimStart('-').Replace(".", string.Empty, StringComparison.Ordinal).TrimStart('0');
        if (digits.Length > MaxSignificantDigits) return new FormCatalogCell(canonical);
        var point = canonical.IndexOf('.', StringComparison.Ordinal);
        var decimals = point < 0 ? 0 : canonical.Length - point - 1;
        var number = decimal.Parse(canonical, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        return new FormCatalogCell(null, Number: number, Format: decimals == 0 ? "#,##0" : "#,##0." + new string('0', decimals));
    }

    /// <summary>
    /// La celda de un campo: el número y la fecha como el valor de la E12, sí/no con su palabra, una opción con su texto o su código
    /// (<see cref="FormCatalogFieldOptions.ExportText"/>) y un texto como es; sin valor, vacía.
    /// </summary>
    private static FormCatalogCell FieldCell(FormCatalogField field, IReadOnlyDictionary<string, JsonElement> values,
        FormCatalogExportFields fields, CultureProfile profile)
    {
        if (!values.TryGetValue(field.Key, out var value)) return FormCatalogCell.Blank;
        var text = value.ValueKind == JsonValueKind.String ? value.GetString()! : null;
        return field.Type switch
        {
            FormCatalogFieldType.WholeNumber when text is not null => ValueCell(text, FormCatalogValueType.WholeNumber, profile),
            FormCatalogFieldType.DecimalNumber when text is not null => ValueCell(text, FormCatalogValueType.DecimalNumber, profile),
            FormCatalogFieldType.Date when text is not null => ValueCell(text, FormCatalogValueType.Date, profile),
            FormCatalogFieldType.YesNo when value.ValueKind is JsonValueKind.True or JsonValueKind.False =>
                new FormCatalogCell(value.GetBoolean() ? fields.Yes : fields.No),
            FormCatalogFieldType.Catalog when Guid.TryParse(text, out var optionId) => new FormCatalogCell(fields.Options.ExportText(field.SourceCatalogId, optionId)),
            _ => new FormCatalogCell(text ?? value.GetRawText()),
        };
    }

    private static FormCatalogCell[] Cells(string code, string text, string? value, string? detail, FormCatalogValueType valueType,
        CultureProfile profile, IReadOnlyList<FormCatalogCell> fieldCells) => valueType == FormCatalogValueType.Text
        ? [new(code), new(text), .. fieldCells, new(detail)]
        : [new(code), new(text), ValueCell(value, valueType, profile), .. fieldCells, new(detail)];
}

/// <summary>
/// Los campos activos que se exportan (E13), en su orden, con las opciones de sus fuentes y las palabras de sí y no en la cultura
/// del pedido.
/// </summary>
internal sealed record FormCatalogExportFields(IReadOnlyList<FormCatalogField> Fields, FormCatalogFieldOptions Options, string Yes, string No)
{
    public override string ToString() => nameof(FormCatalogExportFields);
}
