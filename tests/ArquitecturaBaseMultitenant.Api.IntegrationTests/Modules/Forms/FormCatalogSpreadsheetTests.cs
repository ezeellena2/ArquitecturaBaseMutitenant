using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Api.Modules.Forms.Controllers;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Forms;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure.Spreadsheets;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Forms;

/// <summary>
/// La planilla de los catálogos (C8 a C10) con ClosedXML, sin base: los topes se cuentan antes de cargar el libro (lo
/// descomprimido de los estilos y los textos compartidos, las celdas vacías con formato y el área de una fórmula matricial), el
/// CSV es UTF-8 estricto y lee comillas con saltos de línea, más de 64 columnas, un registro más largo que el tope de línea, dos
/// hojas que apuntan a la misma parte y más hojas que el tope pasan los topes, y lo exportado se vuelve a importar
/// sin diferencias en español y en inglés, con un número de 16 cifras y una fecha de 1899 como texto canónico.
/// </summary>
public sealed partial class FormCatalogSpreadsheetTests
{
    private static readonly FormCatalogSpreadsheet Spreadsheet = new();
    private static readonly FormCatalogFileLimits Limits = FormCatalogFileResolver.Limits;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("xl/styles.xml")]
    [InlineData("xl/sharedStrings.xml")]
    public void A_workbook_whose_styles_or_shared_strings_inflate_past_the_limit_is_over_limits(string part)
    {
        var workbook = Workbook(sheet =>
        {
            Header(sheet);
            sheet.Cell(2, 1).SetValue("A");
            sheet.Cell(2, 2).SetValue("Uno");
        });
        Assert.Single(Spreadsheet.Read("catalogo.xlsx", workbook, Limits).Rows);

        var inflated = Inflate(workbook, part, Limits.MaxUncompressedBytes);

        // Comprimido sigue chico: lo que lo frena es lo que ocupa descomprimido.
        Assert.True(inflated.Length < FormCatalogLimits.MaxFileBytes);
        Assert.Equal(FormCatalogSheetStatus.OverLimits, Spreadsheet.Read("catalogo.xlsx", inflated, Limits).Status);
    }

    [Fact]
    public void A_sheet_outside_the_worksheets_folder_counts_its_cells_and_too_many_parts_are_over_limits()
    {
        var workbook = Workbook(sheet =>
        {
            Header(sheet);
            sheet.Cell(2, 1).SetValue("A");
            sheet.Cell(2, 2).SetValue("Uno");
        });
        // La misma hoja en otra carpeta, declarada en [Content_Types].xml y en las relaciones del libro: ClosedXML la carga igual.
        var moved = Rename(workbook, "xl/worksheets/sheet1.xml", "xl/hojas/datos.xml",
            ("/xl/worksheets/sheet1.xml", "/xl/hojas/datos.xml"), ("worksheets/sheet1.xml", "hojas/datos.xml"));
        Assert.Single(Spreadsheet.Read("catalogo.xlsx", moved, Limits).Rows);

        Assert.Equal(FormCatalogSheetStatus.OverLimits, Spreadsheet.Read("catalogo.xlsx", moved, Limits with { MaxCells = 3 }).Status);
        using (var archive = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read))
        {
            Assert.Equal(FormCatalogSheetStatus.OverLimits,
                Spreadsheet.Read("catalogo.xlsx", workbook, Limits with { MaxEntries = archive.Entries.Count - 1 }).Status);
        }
    }

    [Fact]
    public void Empty_cells_with_a_format_count_against_the_cells_although_only_the_header_has_content()
    {
        var workbook = Workbook(sheet =>
        {
            Header(sheet);
            sheet.Range("A2:B40").Style.Fill.BackgroundColor = XLColor.Yellow;
        });

        var read = Spreadsheet.Read("catalogo.xlsx", workbook, Limits);
        Assert.Equal(FormCatalogSheetStatus.Read, read.Status);
        Assert.Empty(read.Rows);
        Assert.Equal(FormCatalogSheetStatus.OverLimits, Spreadsheet.Read("catalogo.xlsx", workbook, Limits with { MaxCells = 50 }).Status);
    }

    [Fact]
    public void An_array_formula_counts_its_whole_area_although_it_is_written_in_one_cell()
    {
        var workbook = ReplaceSheet(Workbook(Header), xml => FirstRowEnd().Replace(xml, match =>
        {
            var prefix = match.Groups["prefix"].Value;
            return $"""<{prefix}c r="C1"><{prefix}f t="array" ref="C1:C400">1</{prefix}f><{prefix}v>1</{prefix}v></{prefix}c>{match.Value}""";
        }, 1));

        Assert.Equal(FormCatalogSheetStatus.OverLimits, Spreadsheet.Read("catalogo.xlsx", workbook, Limits with { MaxCells = 100 }).Status);

        var hugeArray = ReplaceSheet(Workbook(Header), xml => FirstRowEnd().Replace(xml, match =>
        {
            var prefix = match.Groups["prefix"].Value;
            return $"""<{prefix}c r="C1"><{prefix}f t="array" ref="A1:ZZ100000">1</{prefix}f><{prefix}v>1</{prefix}v></{prefix}c>{match.Value}""";
        }, 1));

        Assert.Equal(FormCatalogSheetStatus.OverLimits, Spreadsheet.Read("catalogo.xlsx", hugeArray, Limits).Status);
    }

    [Fact]
    public void A_sheet_outside_worksheets_declared_by_workbook_relationships_is_rejected_without_opening_the_workbook()
    {
        var moved = Rename(Workbook(Header), "xl/worksheets/sheet1.xml", "xl/hojas/datos.xml",
            ("/xl/worksheets/sheet1.xml", "/xl/hojas/datos.xml"), ("worksheets/sheet1.xml", "hojas/datos.xml"));
        var withHugeArray = ReplacePart(moved, "xl/hojas/datos.xml", xml => FirstRowEnd().Replace(xml, match =>
        {
            var prefix = match.Groups["prefix"].Value;
            return $"""<{prefix}c r="C1"><{prefix}f t="array" ref="A1:ZZ100000">1</{prefix}f><{prefix}v>1</{prefix}v></{prefix}c>{match.Value}""";
        }, 1));
        var hiddenFromContentTypes = ReplacePart(withHugeArray, "[Content_Types].xml",
            xml => xml.Replace("application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml", "application/xml", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(FormCatalogSheetStatus.OverLimits, Spreadsheet.Read("catalogo.xlsx", withHugeArray, Limits).Status);
        Assert.Equal(FormCatalogSheetStatus.OverLimits, Spreadsheet.Read("catalogo.xlsx", hiddenFromContentTypes, Limits).Status);
    }

    /// <summary>ClosedXML carga una hoja por cada <c>&lt;sheet&gt;</c>, aunque dos apunten a la misma parte: no pasa ninguno de los dos.</summary>
    [Fact]
    public void Two_sheets_pointing_to_the_same_part_or_more_sheets_than_the_cap_are_over_limits()
    {
        static void Options(IXLWorksheet sheet)
        {
            Header(sheet);
            sheet.Cell(2, 1).SetValue("A");
            sheet.Cell(2, 2).SetValue("Uno");
        }
        var twoSheets = Workbook(Options, extraSheets: 1);
        Assert.Single(Spreadsheet.Read("catalogo.xlsx", twoSheets, Limits).Rows);

        var repeated = ReplacePart(Workbook(Options), "xl/workbook.xml", xml => SheetElement().Replace(xml, match =>
            string.Concat(Enumerable.Range(1, 2).Select(index =>
                $"""<{match.Groups["prefix"].Value}sheet name="H{index}" sheetId="{index}" {match.Groups["attr"].Value}="{match.Groups["id"].Value}"/>""")), 1));
        Assert.Equal(FormCatalogSheetStatus.OverLimits, Spreadsheet.Read("catalogo.xlsx", repeated, Limits).Status);

        var tooMany = Workbook(Options, extraSheets: SpreadsheetScanner.DefaultMaxSheets);
        Assert.Equal(FormCatalogSheetStatus.OverLimits, Spreadsheet.Read("catalogo.xlsx", tooMany, Limits).Status);
    }

    [Fact]
    public async Task A_zip_with_twenty_thousand_parts_and_a_twelve_mib_file_are_rejected_without_opening_the_workbook()
    {
        var zipWithTooManyParts = ZipWithEntries(20_000);
        Assert.Equal(FormCatalogSheetStatus.OverLimits, Spreadsheet.Read("catalogo.xlsx", zipWithTooManyParts, Limits).Status);

        var twelveMiBBytes = new byte[12 * 1024 * 1024];
        Assert.Equal(FormCatalogSheetStatus.OverLimits, Spreadsheet.Read("catalogo.xlsx", twelveMiBBytes, Limits).Status);

        using var twelveMiBStream = new BoundedReadStream(12L * 1024 * 1024);
        var resolved = await new FormCatalogFileResolver(Spreadsheet, new JsonReferenceDataCatalog())
            .ResolveAsync("catalogo.xlsx", twelveMiBStream, FormCatalogValueType.Text, Ct);
        Assert.True(resolved.IsFailure);
        Assert.Equal(FormCatalogImportErrors.FileInvalidCode, resolved.Error.Code);
        Assert.True(twelveMiBStream.TotalRead <= FormCatalogLimits.MaxFileBytes + 81_920,
            $"Leyó {twelveMiBStream.TotalRead} bytes; debía cortar en {FormCatalogLimits.MaxFileBytes}.");
    }

    [Theory]
    [InlineData(nameof(BusinessFormCatalogsController.PreviewImport))]
    [InlineData(nameof(BusinessFormCatalogsController.ApplyImport))]
    public void Multipart_import_endpoints_declare_eleven_mib_request_limits(string actionName)
    {
        const long expectedBytes = 11L * 1024 * 1024;
        var method = typeof(BusinessFormCatalogsController).GetMethod(actionName);
        Assert.NotNull(method);

        var sizeLimit = Assert.Single(method.GetCustomAttributes(inherit: false).OfType<IRequestSizeLimitMetadata>());
        var formLimit = Assert.Single(method.GetCustomAttributes(inherit: false).OfType<RequestFormLimitsAttribute>());
        Assert.Equal(expectedBytes, sizeLimit.MaxRequestBodySize);
        Assert.Equal(expectedBytes, formLimit.MultipartBodyLengthLimit);
    }

    [Fact]
    public void A_csv_that_is_not_utf8_is_invalid()
    {
        var latin1 = Encoding.Latin1.GetBytes("Código;Texto\nA;Canción\n");

        Assert.Equal(FormCatalogSheetStatus.Invalid, Spreadsheet.Read("pegado.csv", latin1, Limits).Status);
        Assert.Equal(FormCatalogSheetStatus.Read, Spreadsheet.Read("pegado.csv", Encoding.UTF8.GetBytes("Código;Texto\nA;Canción\n"), Limits).Status);
    }

    [Fact]
    public void A_csv_separated_by_semicolons_reads_a_quoted_field_with_the_separator_doubled_quotes_and_a_line_break()
    {
        var csv = Encoding.UTF8.GetBytes("﻿Código;Texto;Detalle\r\nA;\"Uno; dos\";\"Primera línea\r\nsegunda \"\"línea\"\"\"\r\n\r\nB;Dos;\r\n");

        var read = Spreadsheet.Read("pegado.csv", csv, Limits);

        Assert.Equal(FormCatalogSheetStatus.Read, read.Status);
        Assert.Equal(["Código", "Texto", "Detalle"], read.Headers);
        Assert.Equal([2, 4], read.Rows.Select(row => row.Number));
        Assert.Equal(["A", "Uno; dos", "Primera línea\r\nsegunda \"línea\""], read.Rows[0].Cells.Select(cell => cell.Text));
        Assert.Equal(["B", "Dos", null], read.Rows[1].Cells.Select(cell => cell.Text));
    }

    [Fact]
    public void More_than_64_columns_is_over_limits_in_a_csv_and_in_a_workbook()
    {
        static byte[] Csv(int columns) => Encoding.UTF8.GetBytes(string.Join(',', Enumerable.Range(1, columns).Select(i => "C" + i)) + "\nA\n");
        static byte[] Xlsx(int columns) => Workbook(sheet =>
        {
            for (var column = 1; column <= columns; column++) sheet.Cell(1, column).SetValue("C" + column);
        });

        Assert.Equal(FormCatalogSheetStatus.Read, Spreadsheet.Read("pegado.csv", Csv(64), Limits).Status);
        Assert.Equal(FormCatalogSheetStatus.OverLimits, Spreadsheet.Read("pegado.csv", Csv(65), Limits).Status);
        Assert.Equal(FormCatalogSheetStatus.Read, Spreadsheet.Read("catalogo.xlsx", Xlsx(64), Limits).Status);
        Assert.Equal(FormCatalogSheetStatus.OverLimits, Spreadsheet.Read("catalogo.xlsx", Xlsx(65), Limits).Status);
    }

    [Fact]
    public void A_csv_record_longer_than_the_line_cap_is_over_limits_also_inside_quotes()
    {
        var cap = SpreadsheetScanner.MaxCsvLineChars;
        Assert.Equal(FormCatalogSheetStatus.Read,
            Spreadsheet.Read("pegado.csv", Encoding.UTF8.GetBytes("Código;Texto\nA;" + new string('x', cap - 2) + "\n"), Limits).Status);

        Assert.Equal(FormCatalogSheetStatus.OverLimits,
            Spreadsheet.Read("pegado.csv", Encoding.UTF8.GetBytes("Código;Texto\nA;" + new string('x', cap) + "\n"), Limits).Status);
        // Un campo entre comillas con saltos de línea es un solo registro: el tope cuenta el registro entero.
        var quotedLines = string.Join("\n", Enumerable.Repeat(new string('x', 1_000), cap / 1_000 + 1));
        Assert.Equal(FormCatalogSheetStatus.OverLimits,
            Spreadsheet.Read("pegado.csv", Encoding.UTF8.GetBytes($"Código;Texto\nA;\"{quotedLines}\"\n"), Limits).Status);
        Assert.Equal(FormCatalogSheetStatus.OverLimits,
            Spreadsheet.Read("pegado.csv", Encoding.UTF8.GetBytes(new string(';', 10_000_000)), Limits).Status);
    }

    [Theory]
    [InlineData("es-AR", FormCatalogValueType.Text)]
    [InlineData("es-AR", FormCatalogValueType.WholeNumber)]
    [InlineData("es-AR", FormCatalogValueType.DecimalNumber)]
    [InlineData("es-AR", FormCatalogValueType.Date)]
    [InlineData("en-US", FormCatalogValueType.Text)]
    [InlineData("en-US", FormCatalogValueType.WholeNumber)]
    [InlineData("en-US", FormCatalogValueType.DecimalNumber)]
    [InlineData("en-US", FormCatalogValueType.Date)]
    public async Task What_is_exported_imports_back_without_differences_in_every_culture(string culture, FormCatalogValueType valueType)
    {
        using var scope = new CultureScope(culture);
        var cultures = new JsonReferenceDataCatalog();
        var profile = await new CultureProfiles(cultures).LoadAsync(culture, Ct);
        var saved = Saved(valueType);

        var content = await Spreadsheet.WriteAsync(FormCatalogExportBuilder.Layout(valueType, withNotes: false),
            Rows(saved, valueType, profile), Ct);

        var sheet = Spreadsheet.Read("Talles.xlsx", content, Limits);
        Assert.Equal(FormCatalogSheetStatus.Read, sheet.Status);
        Assert.Equal(saved.Length, sheet.Rows.Count);
        if (valueType != FormCatalogValueType.Text)
        {
            // La celda del valor es la tercera: un número o una fecha de Excel conservan su valor, y lo que Excel no guarda
            // bien (16 cifras, antes de 1900) viaja como su texto canónico.
            var values = sheet.Rows.Select(row => row.Cells[2]).ToArray();
            if (valueType == FormCatalogValueType.Date)
            {
                Assert.Equal(new DateOnly(2026, 3, 31), values[0].Date);
                Assert.Equal(("1899-12-31", (DateOnly?)null), (values[2].Text, values[2].Date));
            }
            else
            {
                Assert.Equal(saved[0].Value, FormCatalogValueText.Number(values[0].Number!.Value));
                Assert.Equal(("1234567890123456", (decimal?)null), (values[2].Text, values[2].Number));
            }
        }

        var file = await new FormCatalogFileResolver(Spreadsheet, cultures).ResolveAsync("Talles.xlsx", new MemoryStream(content),
            valueType, Ct);
        Assert.True(file.IsSuccess, file.IsFailure ? file.Error.Code : null);
        var plan = FormCatalogImportPolicy.Decide(valueType, FormCatalogImportMode.Replace, file.Value, saved, saved.Length);

        Assert.Equal((saved.Length, saved.Length, 0), (plan.RowsRead, plan.Unchanged, plan.ErrorRows));
        Assert.Empty(plan.Lists.Errors);
        Assert.Empty(plan.Lists.Changed);
        Assert.Empty(plan.Writes);
        Assert.Empty(plan.Deactivations);
    }

    /// <summary>
    /// O14: la plantilla, con sus notas y su fila de ejemplo, nombra las columnas igual que la exportación, y su ejemplo se importa
    /// como una opción nueva, sin errores, en cada idioma y cada tipo.
    /// </summary>
    [Theory]
    [InlineData("es-AR", FormCatalogValueType.Text)]
    [InlineData("es-AR", FormCatalogValueType.WholeNumber)]
    [InlineData("es-AR", FormCatalogValueType.DecimalNumber)]
    [InlineData("es-AR", FormCatalogValueType.Date)]
    [InlineData("en-US", FormCatalogValueType.Text)]
    [InlineData("en-US", FormCatalogValueType.WholeNumber)]
    [InlineData("en-US", FormCatalogValueType.DecimalNumber)]
    [InlineData("en-US", FormCatalogValueType.Date)]
    public async Task The_template_names_the_columns_like_the_export_and_its_example_imports_as_a_new_option(string culture,
        FormCatalogValueType valueType)
    {
        using var scope = new CultureScope(culture);
        var cultures = new JsonReferenceDataCatalog();
        var profile = await new CultureProfiles(cultures).LoadAsync(culture, Ct);

        var template = await Spreadsheet.WriteAsync(FormCatalogExportBuilder.Layout(valueType, withNotes: true),
            Rows([FormCatalogExportBuilder.Example(valueType, profile)]), Ct);
        var export = await Spreadsheet.WriteAsync(FormCatalogExportBuilder.Layout(valueType, withNotes: false),
            Rows(Saved(valueType), valueType, profile), Ct);

        var templateSheet = Spreadsheet.Read("plantilla.xlsx", template, Limits);
        Assert.Equal(FormCatalogSheetStatus.Read, templateSheet.Status);
        Assert.Equal(Spreadsheet.Read("Talles.xlsx", export, Limits).Headers, templateSheet.Headers);
        Assert.Single(templateSheet.Rows);

        var file = await new FormCatalogFileResolver(Spreadsheet, cultures).ResolveAsync("plantilla.xlsx", new MemoryStream(template),
            valueType, Ct);
        Assert.True(file.IsSuccess, file.IsFailure ? file.Error.Code : null);
        var plan = FormCatalogImportPolicy.Decide(valueType, FormCatalogImportMode.Replace, file.Value, [], 0);

        Assert.Empty(plan.Lists.Errors);
        Assert.Equal(0, plan.ErrorRows);
        Assert.Single(plan.Lists.Added);
        Assert.Single(plan.Writes);
    }

    private static FormCatalogStoredItemRow[] Saved(FormCatalogValueType valueType)
    {
        string?[] values = valueType switch
        {
            FormCatalogValueType.WholeNumber => ["1500", "-42", "1234567890123456", "0"],
            FormCatalogValueType.DecimalNumber => ["2500.25", "-0.5", "1234567890123456", "0.1234567891"],
            FormCatalogValueType.Date => ["2026-03-31", "1900-03-01", "1899-12-31", "1900-02-28"],
            _ => [null, null, null, null],
        };
        (string Code, string Text, string? Detail)[] items = [("S", "Chico", "Hasta 40"), ("001", "Uno", null), ("M-2", "Mediano", "Ñandú"),
            ("L", "Grande", null)];
        return [.. items.Select((item, index) => new FormCatalogStoredItemRow(Guid.CreateVersion7(), item.Code,
            FormCatalogItem.NormalizeCode(item.Code), item.Text, item.Detail, values[index], index, true))];
    }

    private static async IAsyncEnumerable<IReadOnlyList<FormCatalogCell>> Rows(IEnumerable<FormCatalogStoredItemRow> items,
        FormCatalogValueType valueType, CultureProfile profile)
    {
        await Task.CompletedTask;
        foreach (var item in items) yield return FormCatalogExportBuilder.Row(item, valueType, profile);
    }

    private static async IAsyncEnumerable<IReadOnlyList<FormCatalogCell>> Rows(IEnumerable<IReadOnlyList<FormCatalogCell>> rows)
    {
        await Task.CompletedTask;
        foreach (var row in rows) yield return row;
    }

    private static void Header(IXLWorksheet sheet)
    {
        sheet.Cell(1, 1).SetValue("Código");
        sheet.Cell(1, 2).SetValue("Texto");
    }

    private static byte[] Workbook(Action<IXLWorksheet> fill, int extraSheets = 0)
    {
        using var workbook = new XLWorkbook();
        fill(workbook.AddWorksheet("Opciones"));
        for (var index = 1; index <= extraSheets; index++) workbook.AddWorksheet($"H{index}");
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>Copia el libro y agrega espacios al final de una parte, de a un MiB, hasta pasar los bytes pedidos.</summary>
    private static byte[] Inflate(byte[] workbook, string part, long bytes)
    {
        var blank = new byte[1024 * 1024];
        Array.Fill(blank, (byte)' ');
        return Copy(workbook, (name, target) =>
        {
            if (name != part) return;
            for (long written = 0; written <= bytes; written += blank.Length) target.Write(blank);
        }, part);
    }

    private static byte[] ReplaceSheet(byte[] workbook, Func<string, string> replace)
    {
        using var archive = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read);
        var sheet = archive.Entries.Single(entry => entry.FullName.StartsWith("xl/worksheets/", StringComparison.Ordinal)
            && entry.FullName.EndsWith(".xml", StringComparison.Ordinal)).FullName;
        using var reader = new StreamReader(archive.GetEntry(sheet)!.Open(), Encoding.UTF8);
        var replaced = replace(reader.ReadToEnd());
        return Copy(workbook, (name, target) =>
        {
            if (name == sheet) target.Write(Encoding.UTF8.GetBytes(replaced));
        }, sheet, skipOriginal: true);
    }

    /// <summary>Copia el libro con una parte en otra ruta y los textos de las partes XML reemplazados (los tipos y las relaciones).</summary>
    private static byte[] Rename(byte[] workbook, string from, string to, params (string Old, string New)[] references)
    {
        using var output = new MemoryStream();
        using (var source = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read))
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

    /// <summary>Copia cada parte del libro; <paramref name="extend"/> escribe después de la parte (o en su lugar).</summary>
    private static byte[] Copy(byte[] workbook, Action<string, Stream> extend, string part, bool skipOriginal = false)
    {
        using var output = new MemoryStream();
        using (var source = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read))
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Assert.Contains(source.Entries, entry => entry.FullName == part);
            foreach (var entry in source.Entries)
            {
                using var to = target.CreateEntry(entry.FullName, CompressionLevel.Optimal).Open();
                if (!(skipOriginal && entry.FullName == part))
                {
                    using var from = entry.Open();
                    from.CopyTo(to);
                }
                extend(entry.FullName, to);
            }
        }
        return output.ToArray();
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

    [GeneratedRegex(@"<(?<prefix>(?:\w+:)?)sheet\s[^>]*?\b(?<attr>\w+:id)=""(?<id>[^""]+)""[^>]*/>", RegexOptions.CultureInvariant)]
    private static partial Regex SheetElement();

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

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

        public CultureScope(string culture)
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _uiCulture;
        }
    }
}
