using System.Globalization;
using System.Text;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ClosedXML.Excel;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;

/// <summary>
/// La planilla de contactos: .xlsx con ClosedXML (MIT, decisión 10 de la E11) o .csv separado por comas o punto y coma, como
/// lo guarda Excel en español. Lee la primera hoja; un número guardado como número vuelve como sus dígitos.
/// </summary>
internal sealed class WhatsAppContactSpreadsheet : IWhatsAppContactSpreadsheet
{
    public byte[] WriteSample(string sheetName, IReadOnlyList<string> headers, IReadOnlyList<string> example)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet(sheetName);
        for (var column = 0; column < headers.Count; column++)
        {
            sheet.Cell(1, column + 1).SetValue(headers[column]);
            sheet.Cell(2, column + 1).SetValue(column < example.Count ? example[column] : string.Empty);
        }
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<WhatsAppSpreadsheet?> ReadAsync(string? fileName, Stream content, int maxRows, CancellationToken ct)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty).ToUpperInvariant();
        if (extension is not (".XLSX" or ".CSV")) return null;
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        try { return extension == ".CSV" ? ReadCsv(buffer, maxRows) : ReadWorkbook(buffer, maxRows); }
        catch (Exception exception) when (exception is not OperationCanceledException) { return null; }
    }

    private static WhatsAppSpreadsheet? ReadWorkbook(Stream stream, int maxRows)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault();
        if (sheet?.LastRowUsed() is not { } lastRow || sheet.LastColumnUsed() is not { } lastColumn) return null;
        var width = lastColumn.ColumnNumber();
        var headers = Enumerable.Range(1, width).Select(column => Text(sheet.Cell(1, column)) ?? string.Empty).ToArray();
        var rows = new List<WhatsAppSpreadsheetRow>();
        for (var row = 2; row <= lastRow.RowNumber() && rows.Count <= maxRows; row++)
            rows.Add(new(row, [.. Enumerable.Range(1, width).Select(column => Text(sheet.Cell(row, column)))]));
        return new(headers, rows);
    }

    private static string? Text(IXLCell cell) => cell.Value.Type switch
    {
        XLDataType.Blank => null,
        // Un teléfono guardado como número: sus dígitos tal cual, sin el formato de la celda.
        XLDataType.Number => cell.Value.ToString(CultureInfo.InvariantCulture),
        _ => cell.GetFormattedString(),
    };

    private static WhatsAppSpreadsheet? ReadCsv(Stream stream, int maxRows)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine)) return null;
        var separator = headerLine.Count(character => character == ';') > headerLine.Count(character => character == ',') ? ';' : ',';
        var headers = Split(headerLine, separator).Select(value => value ?? string.Empty).ToArray();
        var rows = new List<WhatsAppSpreadsheetRow>();
        var number = 1;
        while (reader.ReadLine() is { } line && rows.Count <= maxRows)
        {
            number++;
            rows.Add(new(number, Split(line, separator)));
        }
        return new(headers, rows);
    }

    /// <summary>Un renglón de CSV con comillas dobles para los campos que llevan el separador.</summary>
    private static string?[] Split(string line, char separator)
    {
        var cells = new List<string?>();
        var current = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"' && quoted && index + 1 < line.Length && line[index + 1] == '"') { current.Append('"'); index++; }
            else if (character == '"') quoted = !quoted;
            else if (character == separator && !quoted) { cells.Add(current.Length == 0 ? null : current.ToString()); current.Clear(); }
            else current.Append(character);
        }
        cells.Add(current.Length == 0 ? null : current.ToString());
        return [.. cells];
    }
}
