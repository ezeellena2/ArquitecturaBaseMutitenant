using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Resources;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;

/// <summary>
/// Lee el archivo o lo pegado (que viaja como un CSV con las columnas pegadas) con sus topes (C8), sin guardar: los bytes con su
/// SHA-256, las columnas reconocidas por su encabezado en cualquier cultura habilitada y, en un catálogo tipado, el valor de cada
/// fila como texto canónico. Un texto se lee con la cultura del pedido y lo que se lee de dos formas queda ambiguo; una celda
/// numérica o de fecha de un XLSX conserva su valor (C9). Qué hace cada fila lo decide <see cref="FormCatalogImportPolicy"/>.
/// </summary>
internal sealed partial class FormCatalogFileResolver(IFormCatalogSpreadsheet spreadsheet, ICultureCatalog cultures)
{
    internal const string CodeColumn = "code";
    internal const string TextColumn = "text";
    internal const string DetailColumn = "detail";
    internal const string ValueColumn = "value";
    private const string HeaderPrefix = "CatalogImport.Header.";

    internal static FormCatalogFileLimits Limits { get; } = new(FormCatalogLimits.MaxFileColumns, FormCatalogLimits.MaxFileRows,
        FormCatalogLimits.MaxFileCells, FormCatalogLimits.MaxFileUncompressedBytes);

    public async Task<Result<FormCatalogFile>> ResolveAsync(string? fileName, Stream content, FormCatalogValueType valueType,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);
        var bytes = await ReadAsync(content, ct);
        if (bytes is null) return FormCatalogImportErrors.FileInvalid(overLimits: true);
        var sheet = spreadsheet.Read(fileName, bytes, Limits);
        if (sheet.Status != FormCatalogSheetStatus.Read)
            return FormCatalogImportErrors.FileInvalid(overLimits: sheet.Status == FormCatalogSheetStatus.OverLimits);

        var profile = await new CultureProfiles(cultures).LoadAsync(CultureInfo.CurrentUICulture.Name, ct);
        var columns = await ColumnsAsync(sheet.Headers, valueType != FormCatalogValueType.Text, ct);
        var rows = sheet.Rows.Select(row => Row(row, columns, valueType, profile.Entry)).ToList();
        return new FormCatalogFile(Convert.ToHexStringLower(SHA256.HashData(bytes)), sheet.SheetName,
            columns.ContainsKey(CodeColumn), columns.ContainsKey(TextColumn), columns.ContainsKey(DetailColumn),
            columns.ContainsKey(ValueColumn), rows);
    }

    /// <summary>
    /// El valor de un texto, leído con la cultura del pedido, como texto canónico (C9). Un número con un solo separador de miles y
    /// sin decimales («1.500» en español) y una fecha cuyo día y mes se pueden cambiar («3/4/2026») quedan ambiguos. El texto
    /// canónico de una exportación (C10), con punto o la fecha ISO, se lee sin ambigüedad.
    /// </summary>
    internal static FormCatalogFileValue ReadValue(string raw, FormCatalogValueType valueType, CultureCatalogEntry culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        var text = raw.Trim();
        return valueType == FormCatalogValueType.Date ? ReadDate(text, culture) : ReadNumber(text, culture);
    }

    /// <summary>Lee hasta 10 MiB más un byte: lo que pasa el tope no se termina de leer (C8).</summary>
    private static async Task<byte[]?> ReadAsync(Stream content, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        int read;
        while ((read = await content.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > FormCatalogLimits.MaxFileBytes) return null;
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    /// <summary>La primera columna de cada dato, por su encabezado en cualquier cultura habilitada; las demás no se leen.</summary>
    private async Task<Dictionary<string, int>> ColumnsAsync(IReadOnlyList<string> headers, bool typed, CancellationToken ct)
    {
        var words = new Dictionary<string, string>(StringComparer.Ordinal);
        var values = new HashSet<string>(StringComparer.Ordinal);
        var profiles = new CultureProfiles(cultures);
        foreach (var entry in (await cultures.ListAsync(ct)).Where(entry => entry.IsEnabled))
        {
            var profile = await profiles.LoadAsync(entry.Code, ct);
            foreach (var column in new[] { CodeColumn, TextColumn, DetailColumn })
                words.TryAdd(Normalize(FormsTexts.Get(HeaderPrefix + Key(column), profile)), column);
            values.Add(Normalize(FormsTexts.Get(HeaderPrefix + "Value", profile)));
        }

        var columns = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < headers.Count; index++)
        {
            var header = Normalize(headers[index]);
            var column = words.GetValueOrDefault(header);
            // «Valor (decimal)», como lo escribe la plantilla, o solo «Valor».
            if (column is null && typed && values.Any(value => header == value || header.StartsWith(value + " (", StringComparison.Ordinal)))
                column = ValueColumn;
            if (column is not null) columns.TryAdd(column, index);
        }
        return columns;
    }

    private static FormCatalogFileRow Row(FormCatalogSheetRow row, Dictionary<string, int> columns, FormCatalogValueType valueType,
        CultureCatalogEntry culture)
    {
        FormCatalogCell? Cell(string column) =>
            columns.TryGetValue(column, out var index) && index < row.Cells.Count ? row.Cells[index] : null;

        FormCatalogFileValue? value = null;
        if (valueType != FormCatalogValueType.Text && columns.ContainsKey(ValueColumn))
            value = Cell(ValueColumn) is { IsBlank: false } cell ? CellValue(cell, valueType, culture) : FormCatalogFileValue.Empty;
        return new FormCatalogFileRow(row.Number, TextOf(Cell(CodeColumn)), TextOf(Cell(TextColumn)), TextOf(Cell(DetailColumn)), value);
    }

    /// <summary>El texto de una celda de código, texto o detalle: un número o una fecha de un XLSX, como su texto canónico.</summary>
    private static string? TextOf(FormCatalogCell? cell)
    {
        if (cell is null || cell.IsBlank) return null;
        if (cell.Number is { } number) return FormCatalogValueText.Number(number);
        if (cell.Date is { } date) return FormCatalogValueText.Date(date);
        return cell.Text!.Trim();
    }

    private static FormCatalogFileValue CellValue(FormCatalogCell cell, FormCatalogValueType valueType, CultureCatalogEntry culture)
    {
        if (cell.Number is { } number)
            return valueType == FormCatalogValueType.Date ? FormCatalogFileValue.Invalid(FormCatalogValueText.Number(number))
                : FormCatalogFileValue.Read(FormCatalogValueText.Number(number));
        if (cell.Date is { } date)
            return valueType == FormCatalogValueType.Date ? FormCatalogFileValue.Read(FormCatalogValueText.Date(date))
                : FormCatalogFileValue.Invalid(FormCatalogValueText.Date(date));
        return ReadValue(cell.Text!, valueType, culture);
    }

    private static FormCatalogFileValue ReadNumber(string text, CultureCatalogEntry culture)
    {
        var group = Regex.Escape(culture.GroupSeparator);
        var point = Regex.Escape(culture.DecimalSeparator);
        var cultural = new Regex($"^[+-]?(?:\\d+|[1-9]\\d{{0,2}}(?:{group}\\d{{3}})+)(?:{point}\\d+)?$", RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
        if (cultural.IsMatch(text))
        {
            // Un solo separador de miles y sin decimales: con los miles es 1500, y leído como canónico, 1,5.
            if (text.Split(culture.GroupSeparator).Length == 2 && !text.Contains(culture.DecimalSeparator, StringComparison.Ordinal))
                return FormCatalogFileValue.Ambiguous(text);
            var canonical = text.TrimStart('+').Replace(culture.GroupSeparator, string.Empty, StringComparison.Ordinal)
                .Replace(culture.DecimalSeparator, ".", StringComparison.Ordinal);
            return FormCatalogFileValue.Read(canonical);
        }
        return CanonicalNumber().IsMatch(text) ? FormCatalogFileValue.Read(text) : FormCatalogFileValue.Invalid(text);
    }

    private static FormCatalogFileValue ReadDate(string text, CultureCatalogEntry culture)
    {
        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso))
            return FormCatalogFileValue.Read(FormCatalogValueText.Date(iso));
        var match = CulturalDate().Match(text);
        if (!match.Success) return FormCatalogFileValue.Invalid(text);
        var first = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var second = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var year = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        if (first != second && first <= 12 && second <= 12) return FormCatalogFileValue.Ambiguous(text);
        var dayFirst = culture.DatePattern.IndexOf('d', StringComparison.Ordinal) < culture.DatePattern.IndexOf('M', StringComparison.Ordinal);
        var (day, month) = dayFirst ? (first, second) : (second, first);
        if (month is < 1 or > 12 || year < 1 || day < 1 || day > DateTime.DaysInMonth(year, month)) return FormCatalogFileValue.Invalid(text);
        return FormCatalogFileValue.Read(FormCatalogValueText.Date(new DateOnly(year, month, day)));
    }

    private static string Key(string column) => column switch
    {
        CodeColumn => "Code",
        TextColumn => "Text",
        _ => "Detail",
    };

    /// <summary>Sin acentos, sin mayúsculas y sin espacios de más: «Código» y «codigo» son el mismo encabezado.</summary>
    private static string Normalize(string? value)
    {
        var decomposed = string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark) builder.Append(char.ToLowerInvariant(character));
        return builder.ToString();
    }

    [GeneratedRegex(@"^-?\d+(?:\.\d+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalNumber();

    [GeneratedRegex(@"^(\d{1,2})[/.-](\d{1,2})[/.-](\d{4})$", RegexOptions.CultureInvariant)]
    private static partial Regex CulturalDate();
}

/// <summary>
/// Lo leído: el SHA-256 de los bytes, la hoja y qué columnas trae; una columna que falta conserva lo guardado (C7).
/// </summary>
internal sealed record FormCatalogFile(string Sha256, string? SheetName, bool HasCode, bool HasText, bool HasDetail, bool HasValue,
    IReadOnlyList<FormCatalogFileRow> Rows)
{
    public override string ToString() => nameof(FormCatalogFile);
}

/// <summary>Una fila con su número en el archivo; <c>null</c> es una celda vacía o una columna que no vino.</summary>
internal sealed record FormCatalogFileRow(int Row, string? Code, string? Text, string? Detail, FormCatalogFileValue? Value)
{
    public override string ToString() => nameof(FormCatalogFileRow);
}

/// <summary>El valor de una fila: su texto canónico, o vacío, ambiguo o inválido con lo que decía la celda.</summary>
internal sealed record FormCatalogFileValue(string? Canonical, string? ErrorCode, string? Raw)
{
    public static FormCatalogFileValue Empty { get; } = new(null, null, null);
    public bool IsEmpty => Canonical is null && ErrorCode is null;
    public static FormCatalogFileValue Read(string canonical) => new(canonical, null, null);
    public static FormCatalogFileValue Ambiguous(string raw) => new(null, FormCatalogImportErrors.ValueAmbiguousCode, raw);
    public static FormCatalogFileValue Invalid(string raw) => new(null, FormCatalogErrors.ValueInvalidCode, raw);

    public override string ToString() => nameof(FormCatalogFileValue);
}
