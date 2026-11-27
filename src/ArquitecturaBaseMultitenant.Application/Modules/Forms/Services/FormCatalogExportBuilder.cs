using System.Globalization;
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
/// su texto canónico, que se reimporta sin ambigüedad.
/// </summary>
internal static class FormCatalogExportBuilder
{
    public const string MediaType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const int MaxSignificantDigits = 15;
    private static readonly DateOnly FirstExcelDate = new(1900, 3, 1);

    /// <summary>Los encabezados; la plantilla dice en la nota de cada uno si es obligatorio.</summary>
    public static FormCatalogSheetLayout Layout(FormCatalogValueType valueType, bool withNotes)
    {
        FormCatalogSheetHeader Header(string key, bool required) =>
            new(FormsTexts.Get(key), withNotes ? FormsTexts.Get(required ? "CatalogImport.Required" : "CatalogImport.Optional") : null);

        var headers = new List<FormCatalogSheetHeader>
        {
            Header("CatalogImport.Header.Code", required: true),
            Header("CatalogImport.Header.Text", required: true),
        };
        if (valueType != FormCatalogValueType.Text) headers.Add(Header("CatalogImport.Header.Value." + valueType, required: true));
        headers.Add(Header("CatalogImport.Header.Detail", required: false));
        return new FormCatalogSheetLayout(FormsTexts.Get("CatalogImport.SheetName"), headers);
    }

    public static IReadOnlyList<FormCatalogCell> Row(FormCatalogStoredItemRow item, FormCatalogValueType valueType, CultureProfile profile)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Cells(item.Code, item.Text, item.Value, item.Detail, valueType, profile);
    }

    /// <summary>La fila de ejemplo de la plantilla, con un valor del tipo.</summary>
    public static IReadOnlyList<FormCatalogCell> Example(FormCatalogValueType valueType, CultureProfile profile)
    {
        var value = valueType switch
        {
            FormCatalogValueType.WholeNumber => "1500",
            FormCatalogValueType.DecimalNumber => "2500.5",
            FormCatalogValueType.Date => "2026-03-31",
            _ => null,
        };
        return Cells(FormsTexts.Get("CatalogImport.Example.Code"), FormsTexts.Get("CatalogImport.Example.Text"), value,
            FormsTexts.Get("CatalogImport.Example.Detail"), valueType, profile);
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

    private static FormCatalogCell[] Cells(string code, string text, string? value, string? detail, FormCatalogValueType valueType,
        CultureProfile profile) => valueType == FormCatalogValueType.Text
        ? [new(code), new(text), new(detail)]
        : [new(code), new(text), ValueCell(value, valueType, profile), new(detail)];
}
