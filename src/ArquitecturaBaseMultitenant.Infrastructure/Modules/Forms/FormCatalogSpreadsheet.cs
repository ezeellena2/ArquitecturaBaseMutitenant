using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Integrations;
using ClosedXML.Excel;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Forms;

/// <summary>
/// La planilla de las opciones de un catálogo: .xlsx con ClosedXML (P12, que se adopta en la E12) o .csv en UTF-8 estricto,
/// separado por comas, punto y coma o tabulaciones, con comillas dobles como lo guarda Excel. Antes de cargar un libro cuenta lo
/// descomprimido y las celdas de cada hoja leyendo el XML de a poco, así una celda vacía con formato o una fórmula matricial no
/// pasan los topes (C8).
/// </summary>
internal sealed class FormCatalogSpreadsheet : IFormCatalogSpreadsheet
{
    private const int SizedRows = 200;
    private static readonly char[] Separators = [',', ';', '\t'];

    public async Task<byte[]> WriteAsync(FormCatalogSheetLayout layout, IAsyncEnumerable<IReadOnlyList<FormCatalogCell>> rows,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(rows);
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet(layout.Name);
        for (var column = 0; column < layout.Headers.Count; column++)
        {
            var header = layout.Headers[column];
            var cell = sheet.Cell(1, column + 1);
            cell.SetValue(header.Text);
            if (header.Note is not null) cell.CreateComment().AddText(header.Note);
        }
        sheet.Row(1).Style.Font.Bold = true;

        var number = 2;
        await foreach (var row in rows.WithCancellation(ct))
        {
            for (var column = 0; column < row.Count; column++) Write(sheet.Cell(number, column + 1), row[column]);
            number++;
        }
        sheet.Columns(1, Math.Max(1, layout.Headers.Count)).AdjustToContents(1, SizedRows);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public FormCatalogSheetRead Read(string? fileName, byte[] content, FormCatalogFileLimits limits)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(limits);
        var extension = Path.GetExtension(fileName ?? string.Empty).ToUpperInvariant();
        try
        {
            return extension switch
            {
                ".XLSX" => ReadWorkbook(content, limits),
                ".CSV" => ReadCsv(content, limits),
                _ => FormCatalogSheetRead.Invalid,
            };
        }
        catch (OverLimitsException) { return FormCatalogSheetRead.OverLimits; }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            return FormCatalogSheetRead.Invalid;
        }
    }

    private static void Write(IXLCell cell, FormCatalogCell value)
    {
        if (value.Number is { } number)
        {
            cell.SetValue((double)number);
            cell.Style.NumberFormat.Format = value.Format ?? "General";
        }
        else if (value.Date is { } date)
        {
            cell.SetValue(date.ToDateTime(TimeOnly.MinValue));
            cell.Style.NumberFormat.Format = value.Format ?? "yyyy-mm-dd";
        }
        else if (value.Text is not null) cell.SetValue(value.Text);
    }

    private static FormCatalogSheetRead ReadWorkbook(byte[] content, FormCatalogFileLimits limits)
    {
        using (var archive = new MemoryStream(content, writable: false)) Scan(archive, limits);
        using var stream = new MemoryStream(content, writable: false);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault();
        if (sheet is null) return FormCatalogSheetRead.Invalid;
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var width = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        if (width > limits.MaxColumns || lastRow - 1 > limits.MaxRows) return FormCatalogSheetRead.OverLimits;

        var headers = Enumerable.Range(1, width).Select(column => Cell(sheet.Cell(1, column)).Text ?? string.Empty).ToArray();
        var rows = new List<FormCatalogSheetRow>();
        for (var row = 2; row <= lastRow; row++)
        {
            var cells = Enumerable.Range(1, width).Select(column => Cell(sheet.Cell(row, column))).ToArray();
            if (!cells.All(cell => cell.IsBlank)) rows.Add(new FormCatalogSheetRow(row, cells));
        }
        return new FormCatalogSheetRead(FormCatalogSheetStatus.Read, sheet.Name, headers, rows);
    }

    /// <summary>Una fórmula cuenta por su valor guardado; un número o una fecha conservan su valor, sin la cultura (C9).</summary>
    private static FormCatalogCell Cell(IXLCell cell)
    {
        var value = cell.HasFormula ? cell.CachedValue : cell.Value;
        return value.Type switch
        {
            XLDataType.Blank => FormCatalogCell.Blank,
            XLDataType.Text => new FormCatalogCell(value.GetText()),
            XLDataType.Number when Math.Abs(value.GetNumber()) < 7.9e28 => new FormCatalogCell(null, Number: (decimal)value.GetNumber()),
            XLDataType.DateTime => new FormCatalogCell(null, Date: DateOnly.FromDateTime(value.GetDateTime())),
            _ => new FormCatalogCell(value.ToString(CultureInfo.InvariantCulture)),
        };
    }

    /// <summary>
    /// Lo descomprimido de cada parte, contado mientras se lee (un encabezado del zip puede mentir), y las celdas de todas las
    /// hojas: cada <c>&lt;c&gt;</c>, también vacía con formato, y el área entera de cada fórmula matricial.
    /// </summary>
    private static void Scan(Stream content, FormCatalogFileLimits limits)
    {
        using var zip = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
        if (zip.Entries.Sum(entry => entry.Length) > limits.MaxUncompressedBytes) throw new OverLimitsException();
        var budget = new Budget(limits.MaxUncompressedBytes);
        long cells = 0;
        foreach (var entry in zip.Entries)
        {
            using var part = new BudgetStream(entry.Open(), budget);
            if (entry.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase)
                && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                cells += CountCells(part);
            else part.CopyTo(Stream.Null);
            if (cells > limits.MaxCells) throw new OverLimitsException();
        }
    }

    private static long CountCells(Stream part)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreComments = true };
        using var reader = XmlReader.Create(part, settings);
        long cells = 0;
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element) continue;
            if (reader.LocalName == "c") cells++;
            else if (reader.LocalName == "f" && reader.GetAttribute("t") == "array" && reader.GetAttribute("ref") is { } area)
                cells += Math.Max(0, Area(area) - 1);
        }
        return cells;
    }

    /// <summary>Las celdas de un rango como «A1:C10»; una sola celda es 1.</summary>
    private static long Area(string reference)
    {
        var corners = reference.Split(':');
        if (corners.Length != 2) return 1;
        var (firstColumn, firstRow) = Corner(corners[0]);
        var (lastColumn, lastRow) = Corner(corners[1]);
        return (Math.Abs(lastColumn - firstColumn) + 1L) * (Math.Abs(lastRow - firstRow) + 1L);
    }

    private static (long Column, long Row) Corner(string reference)
    {
        long column = 0;
        var index = 0;
        for (; index < reference.Length && char.IsAsciiLetter(reference[index]); index++)
            column = Math.Min(column * 26 + (char.ToUpperInvariant(reference[index]) - 'A' + 1), 1_000_000);
        var row = long.TryParse(reference.AsSpan(index), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : 1;
        return (column, Math.Min(row, 10_000_000));
    }

    /// <summary>UTF-8 estricto: un byte que no es UTF-8 hace inválido el archivo.</summary>
    private static FormCatalogSheetRead ReadCsv(byte[] content, FormCatalogFileLimits limits)
    {
        var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(content);
        if (text.Length > 0 && text[0] == '﻿') text = text[1..];
        if (text.Contains('\0', StringComparison.Ordinal)) return FormCatalogSheetRead.Invalid;
        var firstLine = text.Split('\n', 2)[0];
        var separator = Separators.MaxBy(candidate => firstLine.Count(character => character == candidate));

        var records = new List<string?[]>();
        long cells = 0;
        foreach (var record in Records(text, separator))
        {
            if (record.Length > limits.MaxColumns || records.Count > limits.MaxRows) throw new OverLimitsException();
            cells += record.Length;
            if (cells > limits.MaxCells) throw new OverLimitsException();
            records.Add(record);
        }

        if (records.Count == 0) return new FormCatalogSheetRead(FormCatalogSheetStatus.Read, null, [], []);
        var rows = records.Skip(1)
            .Select((record, index) => new FormCatalogSheetRow(index + 2, [.. record.Select(value => new FormCatalogCell(value))]))
            .Where(row => !row.Cells.All(cell => cell.IsBlank)).ToList();
        return new FormCatalogSheetRead(FormCatalogSheetStatus.Read, null, [.. records[0].Select(value => value ?? string.Empty)], rows);
    }

    /// <summary>Los registros de un CSV: un campo entre comillas puede llevar el separador, comillas dobladas y saltos de línea.</summary>
    private static IEnumerable<string?[]> Records(string text, char separator)
    {
        var fields = new List<string?>();
        var current = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (quoted)
            {
                if (character == '"' && index + 1 < text.Length && text[index + 1] == '"') { current.Append('"'); index++; }
                else if (character == '"') quoted = false;
                else current.Append(character);
            }
            else if (character == '"') quoted = true;
            else if (character == separator) { fields.Add(Field(current)); current.Clear(); }
            else if (character is '\r' or '\n')
            {
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
                fields.Add(Field(current));
                current.Clear();
                yield return [.. fields];
                fields.Clear();
            }
            else current.Append(character);
        }
        if (current.Length > 0 || fields.Count > 0)
        {
            fields.Add(Field(current));
            yield return [.. fields];
        }
    }

    private static string? Field(StringBuilder value) => value.Length == 0 ? null : value.ToString();

    private sealed class Budget(long remaining)
    {
        public void Spend(int bytes)
        {
            remaining -= bytes;
            if (remaining < 0) throw new OverLimitsException();
        }
    }

    /// <summary>Cuenta lo que se descomprime de una parte y corta en cuanto pasa lo que queda del tope.</summary>
    private sealed class BudgetStream(Stream inner, Budget budget) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            budget.Spend(read);
            return read;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class OverLimitsException : Exception
    {
        public OverLimitsException() : base("The file is over the limits.") { }
    }
}
