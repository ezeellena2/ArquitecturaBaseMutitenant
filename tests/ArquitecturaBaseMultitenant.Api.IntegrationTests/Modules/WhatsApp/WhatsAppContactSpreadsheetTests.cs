using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.Controllers;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Spreadsheets;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// La planilla de importación de contactos de WhatsApp pasa por el escáner común antes de abrir ClosedXML: rechaza fórmulas
/// matriciales grandes, hojas fuera de <c>xl/worksheets/</c> que superan los topes, dos hojas que apuntan a la misma parte, más
/// hojas que el tope, ZIPs con 20.000 partes y archivos de 12 MiB sin copiar más de 10 MiB a memoria ni evaluar fórmulas. Un CSV
/// tiene el mismo tope de columnas que el libro y un tope de largo de línea; el ejemplo descargable sigue entrando.
/// </summary>
public sealed partial class WhatsAppContactSpreadsheetTests
{
    private static readonly WhatsAppContactSpreadsheet Spreadsheet = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_array_formula_over_A1_to_ZZ100000_is_rejected_without_opening_the_workbook()
    {
        var attack = ReplaceSheet(Workbook(ContactHeader), xml => FirstRowEnd().Replace(xml, match =>
        {
            var prefix = match.Groups["prefix"].Value;
            return $"""<{prefix}c r="C1"><{prefix}f t="array" ref="A1:ZZ100000">1</{prefix}f><{prefix}v>1</{prefix}v></{prefix}c>{match.Value}""";
        }, 1));

        using (var scanStream = new MemoryStream(attack, writable: false))
            Assert.Throws<SpreadsheetOverLimitsException>(() => SpreadsheetScanner.ScanXlsx(scanStream, WhatsAppContactSpreadsheet.ScanLimits));
        using var readStream = new MemoryStream(attack, writable: false);
        Assert.Null(await Spreadsheet.ReadAsync("contactos.xlsx", readStream, WhatsAppFeature.MaxImportRows, Ct));
    }

    [Fact]
    public async Task A_sheet_saved_outside_worksheets_folder_is_scanned_via_workbook_relationships_and_rejected_when_over_limits()
    {
        var baseWorkbook = Workbook(sheet =>
        {
            ContactHeader(sheet);
            sheet.Cell(2, 1).SetValue("Ana");
            sheet.Cell(2, 2).SetValue("+5491155551234");
        });
        var moved = Rename(baseWorkbook, "xl/worksheets/sheet1.xml", "xl/hojas/datos.xml",
            ("/xl/worksheets/sheet1.xml", "/xl/hojas/datos.xml"), ("worksheets/sheet1.xml", "hojas/datos.xml"));
        using (var validStream = new MemoryStream(moved, writable: false))
        {
            var valid = await Spreadsheet.ReadAsync("contactos.xlsx", validStream, WhatsAppFeature.MaxImportRows, Ct);
            Assert.NotNull(valid);
            Assert.Single(valid.Rows);
        }

        // Con una fórmula matricial dentro de la hoja movida y sin el Override .worksheet+xml en [Content_Types].xml:
        // solo xl/workbook.xml + xl/_rels/workbook.xml.rels la declara como hoja, y el escáner la frena antes de abrir el libro.
        var attackInMovedSheet = ReplacePart(moved, "xl/hojas/datos.xml", xml => FirstRowEnd().Replace(xml, match =>
        {
            var prefix = match.Groups["prefix"].Value;
            return $"""<{prefix}c r="C1"><{prefix}f t="array" ref="A1:ZZ100000">1</{prefix}f><{prefix}v>1</{prefix}v></{prefix}c>{match.Value}""";
        }, 1));
        var hiddenFromContentTypes = ReplacePart(attackInMovedSheet, "[Content_Types].xml",
            xml => xml.Replace("application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml", "application/xml", StringComparison.OrdinalIgnoreCase));

        foreach (var candidate in new[] { attackInMovedSheet, hiddenFromContentTypes })
        {
            using (var scanStream = new MemoryStream(candidate, writable: false))
                Assert.Throws<SpreadsheetOverLimitsException>(() => SpreadsheetScanner.ScanXlsx(scanStream, WhatsAppContactSpreadsheet.ScanLimits));
            using var readStream = new MemoryStream(candidate, writable: false);
            Assert.Null(await Spreadsheet.ReadAsync("contactos.xlsx", readStream, WhatsAppFeature.MaxImportRows, Ct));
        }
    }

    [Fact]
    public async Task A_zip_with_twenty_thousand_parts_is_rejected_without_opening_the_workbook()
    {
        var zipWithTooManyParts = ZipWithEntries(20_000);

        using (var scanStream = new MemoryStream(zipWithTooManyParts, writable: false))
            Assert.Throws<SpreadsheetOverLimitsException>(() => SpreadsheetScanner.ScanXlsx(scanStream, WhatsAppContactSpreadsheet.ScanLimits));
        using var readStream = new MemoryStream(zipWithTooManyParts, writable: false);
        Assert.Null(await Spreadsheet.ReadAsync("contactos.xlsx", readStream, WhatsAppFeature.MaxImportRows, Ct));
    }

    /// <summary>
    /// ClosedXML carga una hoja por cada <c>&lt;sheet&gt;</c> de <c>xl/workbook.xml</c>: si dos apuntan a la misma parte (el mismo
    /// <c>r:id</c>, o dos relaciones con el mismo destino, aunque se escriba distinto), las celdas contadas una vez se cargarían
    /// muchas veces.
    /// </summary>
    [Theory]
    [InlineData("repeated-id")]
    [InlineData("same-target")]
    [InlineData("same-target-written-differently")]
    public async Task Two_sheets_pointing_to_the_same_part_are_rejected_without_opening_the_workbook(string variant)
    {
        var workbook = Workbook(sheet =>
        {
            ContactHeader(sheet);
            sheet.Cell(2, 1).SetValue("Ana");
            sheet.Cell(2, 2).SetValue("+5491155551234");
        });
        var attack = variant == "repeated-id"
            ? RepeatSheet(workbook, copies: 200)
            : SheetWithSecondRelationship(workbook, writtenDifferently: variant == "same-target-written-differently");

        using (var scanStream = new MemoryStream(attack, writable: false))
            Assert.Throws<SpreadsheetOverLimitsException>(() => SpreadsheetScanner.ScanXlsx(scanStream, WhatsAppContactSpreadsheet.ScanLimits));
        using var readStream = new MemoryStream(attack, writable: false);
        Assert.Null(await Spreadsheet.ReadAsync("contactos.xlsx", readStream, WhatsAppFeature.MaxImportRows, Ct));
    }

    [Fact]
    public async Task More_sheets_than_the_cap_are_rejected_and_a_workbook_with_a_second_sheet_still_reads()
    {
        static void Contacts(IXLWorksheet sheet)
        {
            ContactHeader(sheet);
            sheet.Cell(2, 1).SetValue("Ana");
            sheet.Cell(2, 2).SetValue("+5491155551234");
        }

        using (var twoSheets = new MemoryStream(Workbook(Contacts, extraSheets: 1), writable: false))
        {
            var read = await Spreadsheet.ReadAsync("contactos.xlsx", twoSheets, WhatsAppFeature.MaxImportRows, Ct);
            Assert.NotNull(read);
            Assert.Single(read.Rows);
        }

        var tooMany = Workbook(Contacts, extraSheets: WhatsAppContactSpreadsheet.ScanLimits.MaxSheets);
        using (var scanStream = new MemoryStream(tooMany, writable: false))
            Assert.Throws<SpreadsheetOverLimitsException>(() => SpreadsheetScanner.ScanXlsx(scanStream, WhatsAppContactSpreadsheet.ScanLimits));
        using var readStream = new MemoryStream(tooMany, writable: false);
        Assert.Null(await Spreadsheet.ReadAsync("contactos.xlsx", readStream, WhatsAppFeature.MaxImportRows, Ct));
    }

    [Fact]
    public async Task A_csv_with_more_than_64_columns_or_a_line_past_the_cap_is_rejected()
    {
        async Task<WhatsAppSpreadsheet?> ReadCsvAsync(string text)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text), writable: false);
            return await Spreadsheet.ReadAsync("contactos.csv", stream, WhatsAppFeature.MaxImportRows, Ct);
        }
        string[] header = ["Nombre", "Teléfono", .. Enumerable.Range(3, 62).Select(column => "C" + column)];

        var sixtyFour = await ReadCsvAsync(string.Join(';', header) + "\nAna;+5491155551234;" + new string(';', 61) + "\n");
        Assert.NotNull(sixtyFour);
        Assert.Equal(64, sixtyFour.Headers.Count);
        Assert.Equal(64, Assert.Single(sixtyFour.Rows).Cells.Count);

        Assert.Null(await ReadCsvAsync(string.Join(';', header) + ";C65\nAna;+5491155551234\n"));
        Assert.Null(await ReadCsvAsync("Nombre;Teléfono\nAna;+5491155551234;" + new string(';', 62) + "\n"));
        Assert.Null(await ReadCsvAsync("Nombre;Teléfono\nAna;" + new string('1', SpreadsheetScanner.MaxCsvLineChars) + "\n"));
        // Diez millones de separadores en el encabezado (10 MB, bajo el tope del archivo): se corta al leer la línea.
        Assert.Null(await ReadCsvAsync(new string(';', 10_000_000)));
    }

    [Theory]
    [InlineData("contactos.xlsx")]
    [InlineData("contactos.csv")]
    public async Task A_twelve_mib_file_is_rejected_before_copying_past_ten_mib(string fileName)
    {
        using var stream = new BoundedReadStream(12L * 1024 * 1024);

        var result = await Spreadsheet.ReadAsync(fileName, stream, WhatsAppFeature.MaxImportRows, Ct);

        Assert.Null(result);
        Assert.True(stream.TotalRead <= WhatsAppContactSpreadsheet.MaxFileBytes + 81_920,
            $"Leyó {stream.TotalRead} bytes; debía cortar en {WhatsAppContactSpreadsheet.MaxFileBytes}.");
    }

    [Fact]
    public async Task Formula_cells_read_their_cached_value_without_evaluating_the_formula()
    {
        var workbook = ReplaceSheet(Workbook(sheet =>
        {
            ContactHeader(sheet);
            sheet.Cell(2, 1).SetValue("Placeholder");
            sheet.Cell(2, 2).SetValue("Placeholder");
        }), xml => RowTwo().Replace(xml, match =>
        {
            var prefix = match.Groups["prefix"].Value;
            return $"""<{prefix}row r="2"><{prefix}c r="A2" t="str"><{prefix}f>1+1</{prefix}f><{prefix}v>Ana</{prefix}v></{prefix}c><{prefix}c r="B2"><{prefix}f>1/0</{prefix}f><{prefix}v>5491155551234</{prefix}v></{prefix}c></{prefix}row>""";
        }, 1));

        using var stream = new MemoryStream(workbook, writable: false);
        var read = await Spreadsheet.ReadAsync("contactos.xlsx", stream, WhatsAppFeature.MaxImportRows, Ct);

        Assert.NotNull(read);
        var row = Assert.Single(read.Rows);
        Assert.Equal(["Ana", "5491155551234"], row.Cells);
    }

    [Fact]
    public async Task The_import_sample_reads_back_with_its_headers_and_example_row()
    {
        string[] headers = ["Nombre", "Teléfono", "Etiqueta", "Acepta promociones"];
        string[] example = ["Ana Pérez", "+5491155551234", "VIP", "Sí"];

        var sample = Spreadsheet.WriteSample("Contactos", headers, example);
        using var stream = new MemoryStream(sample, writable: false);
        var read = await Spreadsheet.ReadAsync("ejemplo.xlsx", stream, WhatsAppFeature.MaxImportRows, Ct);

        Assert.NotNull(read);
        Assert.Equal(headers, read.Headers);
        var row = Assert.Single(read.Rows);
        Assert.Equal(2, row.Number);
        Assert.Equal(example, row.Cells);
    }

    [Theory]
    [InlineData(nameof(OrganizationWhatsAppContactsController.PreviewImport))]
    [InlineData(nameof(OrganizationWhatsAppContactsController.Import))]
    public void Multipart_import_endpoints_declare_eleven_mib_request_limits(string actionName)
    {
        const long expectedBytes = 11L * 1024 * 1024;
        var method = typeof(OrganizationWhatsAppContactsController).GetMethod(actionName);
        Assert.NotNull(method);

        var sizeLimit = Assert.Single(method.GetCustomAttributes(inherit: false).OfType<IRequestSizeLimitMetadata>());
        var formLimit = Assert.Single(method.GetCustomAttributes(inherit: false).OfType<RequestFormLimitsAttribute>());
        Assert.Equal(expectedBytes, sizeLimit.MaxRequestBodySize);
        Assert.Equal(expectedBytes, formLimit.MultipartBodyLengthLimit);
    }

    private static void ContactHeader(IXLWorksheet sheet)
    {
        sheet.Cell(1, 1).SetValue("Nombre");
        sheet.Cell(1, 2).SetValue("Teléfono");
    }

    private static byte[] Workbook(Action<IXLWorksheet> fill, int extraSheets = 0)
    {
        using var workbook = new XLWorkbook();
        fill(workbook.AddWorksheet("Contactos"));
        for (var index = 1; index <= extraSheets; index++) workbook.AddWorksheet($"H{index}");
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>Reemplaza el único <c>&lt;sheet&gt;</c> del libro por <paramref name="copies"/> con el mismo <c>r:id</c>.</summary>
    private static byte[] RepeatSheet(byte[] workbook, int copies) => ReplacePart(workbook, "xl/workbook.xml", xml =>
        SheetElement().Replace(xml, match => string.Concat(Enumerable.Range(1, copies).Select(index =>
            $"""<{match.Groups["prefix"].Value}sheet name="H{index}" sheetId="{index}" {match.Groups["attr"].Value}="{match.Groups["id"].Value}"/>""")), 1));

    /// <summary>
    /// Suma un segundo <c>&lt;sheet&gt;</c> con otra relación al mismo destino: igual, o escrito con «..» desde la raíz del paquete.
    /// </summary>
    private static byte[] SheetWithSecondRelationship(byte[] workbook, bool writtenDifferently)
    {
        string attr = string.Empty, id = string.Empty;
        var withSheet = ReplacePart(workbook, "xl/workbook.xml", xml => SheetElement().Replace(xml, match =>
        {
            (attr, id) = (match.Groups["attr"].Value, match.Groups["id"].Value);
            return $"""{match.Value}<{match.Groups["prefix"].Value}sheet name="Copia" sheetId="2" {attr}="rIdCopia"/>""";
        }, 1));
        Assert.NotEmpty(id);
        return ReplacePart(withSheet, "xl/_rels/workbook.xml.rels", xml => Regex.Replace(xml,
            $"""<Relationship\b[^>]*\bId="{Regex.Escape(id)}"[^>]*/>""", match =>
            {
                var target = TargetAttribute().Match(match.Value).Groups["target"].Value;
                var path = target.StartsWith('/') ? target[1..] : "xl/" + target;
                var copy = match.Value.Replace($"Id=\"{id}\"", "Id=\"rIdCopia\"", StringComparison.Ordinal);
                return match.Value + (writtenDifferently
                    ? TargetAttribute().Replace(copy, $"Target=\"../{path}\"", 1)
                    : copy);
            }, RegexOptions.CultureInvariant));
    }

    private static byte[] ReplaceSheet(byte[] workbook, Func<string, string> replace)
    {
        string sheetName;
        using (var source = new MemoryStream(workbook, writable: false))
        using (var archive = new ZipArchive(source, ZipArchiveMode.Read))
        {
            sheetName = archive.Entries.Single(entry => entry.FullName.StartsWith("xl/worksheets/", StringComparison.Ordinal)
                && entry.FullName.EndsWith(".xml", StringComparison.Ordinal)).FullName;
        }
        return ReplacePart(workbook, sheetName, replace);
    }

    private static byte[] ReplacePart(byte[] workbook, string partName, Func<string, string> replace)
    {
        using var output = new MemoryStream();
        using (var input = new MemoryStream(workbook, writable: false))
        using (var source = new ZipArchive(input, ZipArchiveMode.Read))
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Assert.Contains(source.Entries, entry => entry.FullName == partName);
            foreach (var entry in source.Entries)
            {
                using var to = target.CreateEntry(entry.FullName, CompressionLevel.Optimal).Open();
                if (entry.FullName == partName)
                {
                    using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
                    var replaced = replace(reader.ReadToEnd());
                    using var writer = new StreamWriter(to, new UTF8Encoding(false));
                    writer.Write(replaced);
                }
                else
                {
                    using var from = entry.Open();
                    from.CopyTo(to);
                }
            }
        }
        return output.ToArray();
    }

    private static byte[] Rename(byte[] workbook, string from, string to, params (string Old, string New)[] references)
    {
        using var output = new MemoryStream();
        using (var input = new MemoryStream(workbook, writable: false))
        using (var source = new ZipArchive(input, ZipArchiveMode.Read))
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Assert.Contains(source.Entries, entry => entry.FullName == from);
            foreach (var entry in source.Entries)
            {
                using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
                var text = reader.ReadToEnd();
                if (entry.FullName.EndsWith(".xml", StringComparison.Ordinal) || entry.FullName.EndsWith(".rels", StringComparison.Ordinal))
                    text = references.Aggregate(text, (current, reference) => current.Replace(reference.Old, reference.New, StringComparison.Ordinal));
                using var writer = new StreamWriter(target.CreateEntry(entry.FullName == from ? to : entry.FullName).Open(), new UTF8Encoding(false));
                writer.Write(text);
            }
        }
        return output.ToArray();
    }

    private static byte[] ZipWithEntries(int count)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (var index = 0; index < count; index++)
                archive.CreateEntry($"p/{index}.txt", CompressionLevel.NoCompression).Open().Dispose();
        }
        return output.ToArray();
    }

    [GeneratedRegex(@"</(?<prefix>\w+:)?row>", RegexOptions.CultureInvariant)]
    private static partial Regex FirstRowEnd();

    [GeneratedRegex(@"<(?<prefix>(?:\w+:)?)row\b[^>]*r=""2""[^>]*>.*?</\k<prefix>row>", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex RowTwo();

    [GeneratedRegex(@"<(?<prefix>(?:\w+:)?)sheet\s[^>]*?\b(?<attr>\w+:id)=""(?<id>[^""]+)""[^>]*/>", RegexOptions.CultureInvariant)]
    private static partial Regex SheetElement();

    [GeneratedRegex(@"\bTarget=""(?<target>[^""]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex TargetAttribute();

    private sealed class BoundedReadStream(long totalLength) : Stream
    {
        private long _remaining = totalLength;
        public long TotalRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_remaining <= 0) return 0;
            var chunk = (int)Math.Min(count, _remaining);
            Array.Fill(buffer, (byte)'A', offset, chunk);
            _remaining -= chunk;
            TotalRead += chunk;
            return chunk;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_remaining <= 0) return ValueTask.FromResult(0);
            var chunk = (int)Math.Min(buffer.Length, _remaining);
            buffer.Span[..chunk].Fill((byte)'A');
            _remaining -= chunk;
            TotalRead += chunk;
            return ValueTask.FromResult(chunk);
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
