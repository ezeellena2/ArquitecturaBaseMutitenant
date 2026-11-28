using System.Globalization;
using System.IO.Compression;
using System.Xml;
using ArquitecturaBaseMultitenant.Application.Models.Common;

namespace ArquitecturaBaseMultitenant.Infrastructure.Spreadsheets;

/// <summary>Topes técnicos para escanear un archivo <c>.xlsx</c> antes de abrirlo con ClosedXML.</summary>
internal sealed record SpreadsheetScanLimits(long MaxUncompressedBytes, long MaxCells, int MaxEntries,
    int MaxSheets = SpreadsheetScanner.DefaultMaxSheets);

/// <summary>Se lanza cuando la planilla supera alguno de los topes de tamaño, partes o celdas.</summary>
internal sealed class SpreadsheetOverLimitsException() : Exception("The spreadsheet is over the limits.");

/// <summary>
/// Escáner común para planillas <c>.xlsx</c> de cualquier módulo (Forms, WhatsApp): valida la cantidad de partes del ZIP, los
/// bytes descomprimidos reales (el encabezado del ZIP puede mentir) y la cantidad de celdas de todas las hojas —declaradas por
/// <c>xl/workbook.xml</c> y sus relaciones o por <c>[Content_Types].xml</c>, estén en la carpeta que estén—, contando celdas vacías
/// con formato y el área entera de fórmulas matriciales antes de abrir <c>XLWorkbook</c>. También topa la cantidad de hojas y
/// rechaza dos hojas que apuntan a la misma parte, que ClosedXML cargaría una vez por hoja. Para un CSV da el tope de largo de línea.
/// </summary>
internal static class SpreadsheetScanner
{
    internal const int DefaultMaxFileBytes = SpreadsheetLimits.MaxFileBytes;

    /// <summary>
    /// Las hojas de un libro (los <c>&lt;sheet&gt;</c> de <c>xl/workbook.xml</c>): 255 es lo más que Excel deja poner en un libro
    /// nuevo, y una planilla de contactos o de opciones trae unas pocas. Las dos importaciones leen solo la primera, pero ClosedXML
    /// arma en memoria cada una.
    /// </summary>
    internal const int DefaultMaxSheets = 255;

    /// <summary>
    /// El largo de una línea de un CSV (de un registro, si lleva saltos de línea entre comillas): 32.768 caracteres alcanzan para
    /// 64 columnas de 512 y para lo que entra en una celda de Excel (32.767). Se corta mientras se lee.
    /// </summary>
    internal const int MaxCsvLineChars = 32_768;

    private const string ContentTypesPart = "[Content_Types].xml";
    private const string PackageRelsPart = "_rels/.rels";
    private const string DefaultWorkbookPart = "xl/workbook.xml";
    private const string OfficeRelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string StrictRelationshipsNamespace = "http://purl.oclc.org/ooxml/officeDocument/relationships";

    private static readonly XmlReaderSettings XmlSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
    };

    /// <summary>Lee el stream de a bloques y corta en cuanto supera <paramref name="maxBytes"/>, sin copiar el resto a memoria.</summary>
    internal static async Task<byte[]?> ReadBoundedAsync(Stream content, int maxBytes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.CanSeek && content.Length - content.Position > maxBytes) return null;
        using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        int read;
        while ((read = await content.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > maxBytes) return null;
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    /// <summary>
    /// Recorre el ZIP entero con presupuesto de bytes descomprimidos y cuenta las celdas de todas las hojas antes de abrir el libro.
    /// Lanza <see cref="SpreadsheetOverLimitsException"/> en cuanto se excede algún tope.
    /// </summary>
    internal static void ScanXlsx(Stream content, SpreadsheetScanLimits limits)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(limits);

        using var zip = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
        if (zip.Entries.Count > limits.MaxEntries) throw new SpreadsheetOverLimitsException();
        CheckDeclaredUncompressedBytes(zip, limits.MaxUncompressedBytes);

        var budget = new Budget(limits.MaxUncompressedBytes);
        var scannedMetadata = new HashSet<ZipArchiveEntry>(ReferenceEqualityComparer.Instance);
        var sheets = DiscoverSheetParts(zip, limits.MaxSheets, budget, scannedMetadata);

        long cells = 0;
        foreach (var entry in zip.Entries)
        {
            var normalized = NormalizePath(entry.FullName);
            var isSheet = sheets.Contains(normalized);
            if (!isSheet && scannedMetadata.Contains(entry)) continue;
            using var part = new BudgetStream(entry.Open(), budget);
            if (isSheet) cells += CountCells(part, limits.MaxCells - cells);
            else part.CopyTo(Stream.Null);
            if (cells > limits.MaxCells) throw new SpreadsheetOverLimitsException();
        }
    }

    private static void CheckDeclaredUncompressedBytes(ZipArchive zip, long maxUncompressedBytes)
    {
        long declaredBytes = 0;
        foreach (var entry in zip.Entries)
        {
            if (entry.Length < 0 || entry.Length > maxUncompressedBytes - declaredBytes)
                throw new SpreadsheetOverLimitsException();
            declaredBytes += entry.Length;
        }
    }

    private static SheetParts DiscoverSheetParts(ZipArchive zip, int maxSheets, Budget budget, HashSet<ZipArchiveEntry> scannedMetadata)
    {
        var overrides = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in FindEntries(zip, ContentTypesPart))
        {
            scannedMetadata.Add(entry);
            using var part = new BudgetStream(entry.Open(), budget);
            ReadContentTypes(part, overrides, extensions);
        }

        var workbookPaths = DiscoverWorkbookPaths(zip, budget, scannedMetadata);
        var workbookSheets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var workbookPath in workbookPaths)
            ReadWorkbookSheets(zip, workbookPath, maxSheets, budget, scannedMetadata, workbookSheets);

        return new SheetParts(workbookSheets, overrides, extensions);
    }

    private static HashSet<string> DiscoverWorkbookPaths(ZipArchive zip, Budget budget, HashSet<ZipArchiveEntry> scannedMetadata)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relsEntry in FindEntries(zip, PackageRelsPart))
        {
            scannedMetadata.Add(relsEntry);
            using var part = new BudgetStream(relsEntry.Open(), budget);
            using var reader = XmlReader.Create(part, XmlSettings);
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "Relationship") continue;
                if (IsExternal(reader)) continue;
                var type = reader.GetAttribute("Type");
                var target = reader.GetAttribute("Target");
                if (target is not null && type?.EndsWith("/officeDocument", StringComparison.OrdinalIgnoreCase) == true)
                    AddResolvedPaths(paths, string.Empty, target);
            }
        }
        if (paths.Count == 0) paths.Add(DefaultWorkbookPart);
        return paths;
    }

    /// <summary>
    /// Las hojas que declara un libro, hasta <paramref name="maxSheets"/>. ClosedXML carga una hoja por cada <c>&lt;sheet&gt;</c>,
    /// aunque dos lleguen a la misma parte (el mismo <c>r:id</c>, o dos relaciones con el mismo destino escrito de otra forma): las
    /// celdas de esa parte, contadas una vez, se cargarían una vez por hoja, así que eso tampoco pasa.
    /// </summary>
    private static void ReadWorkbookSheets(ZipArchive zip, string workbookPath, int maxSheets, Budget budget,
        HashSet<ZipArchiveEntry> scannedMetadata, HashSet<string> workbookSheets)
    {
        var sheetIds = new HashSet<string>(StringComparer.Ordinal);
        var sheets = 0;
        foreach (var workbookEntry in FindEntries(zip, workbookPath))
        {
            scannedMetadata.Add(workbookEntry);
            using var part = new BudgetStream(workbookEntry.Open(), budget);
            using var reader = XmlReader.Create(part, XmlSettings);
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "sheet") continue;
                if (++sheets > maxSheets) throw new SpreadsheetOverLimitsException();
                var relationId = reader.GetAttribute("id", OfficeRelationshipsNamespace)
                    ?? reader.GetAttribute("id", StrictRelationshipsNamespace)
                    ?? reader.GetAttribute("id");
                if (!string.IsNullOrEmpty(relationId) && !sheetIds.Add(relationId)) throw new SpreadsheetOverLimitsException();
            }
        }

        var directory = DirectoryOf(workbookPath);
        var relsPath = RelsPathOf(workbookPath);
        var sheetTargets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relsEntry in FindEntries(zip, relsPath))
        {
            scannedMetadata.Add(relsEntry);
            using var part = new BudgetStream(relsEntry.Open(), budget);
            using var reader = XmlReader.Create(part, XmlSettings);
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "Relationship") continue;
                if (IsExternal(reader)) continue;
                var id = reader.GetAttribute("Id");
                var type = reader.GetAttribute("Type");
                var target = reader.GetAttribute("Target");
                if (target is null) continue;
                var isSheetRelation = id is not null && sheetIds.Contains(id);
                if (isSheetRelation) ClaimSheetTarget(sheetTargets, ResolveTarget(directory, Uri.UnescapeDataString(target)), id!);
                if (isSheetRelation || type?.EndsWith("/worksheet", StringComparison.OrdinalIgnoreCase) == true)
                    AddResolvedPaths(workbookSheets, directory, target);
            }
        }
    }

    /// <summary>Dos <c>&lt;sheet&gt;</c> con relaciones distintas no pueden llegar a la misma parte (sin distinguir mayúsculas, como OPC).</summary>
    private static void ClaimSheetTarget(Dictionary<string, string> sheetTargets, string target, string relationId)
    {
        if (!sheetTargets.TryAdd(target, relationId) && !string.Equals(sheetTargets[target], relationId, StringComparison.Ordinal))
            throw new SpreadsheetOverLimitsException();
    }

    /// <summary>Las partes que <c>[Content_Types].xml</c> declara como hoja, por su nombre o por su extensión.</summary>
    private static void ReadContentTypes(Stream part, Dictionary<string, bool> overrides, HashSet<string> extensions)
    {
        using var reader = XmlReader.Create(part, XmlSettings);
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element) continue;
            var isSheet = reader.GetAttribute("ContentType")?.EndsWith(".worksheet+xml", StringComparison.OrdinalIgnoreCase) == true;
            if (reader.LocalName == "Override" && reader.GetAttribute("PartName") is { } name)
            {
                var normalized = NormalizePath(name);
                overrides[normalized] = isSheet;
                var unescaped = NormalizePath(Uri.UnescapeDataString(name));
                if (!string.Equals(unescaped, normalized, StringComparison.OrdinalIgnoreCase)) overrides[unescaped] = isSheet;
            }
            else if (reader.LocalName == "Default" && isSheet && reader.GetAttribute("Extension") is { } extension)
            {
                extensions.Add(extension);
            }
        }
    }

    private static long CountCells(Stream part, long remainingCells)
    {
        using var reader = XmlReader.Create(part, XmlSettings);
        long cells = 0;
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element) continue;
            if (reader.LocalName == "c") cells++;
            else if (reader.LocalName == "f" && IsFormulaRange(reader.GetAttribute("t")) && reader.GetAttribute("ref") is { } area)
                cells += Math.Max(0, Area(area) - 1);
            if (cells > remainingCells) throw new SpreadsheetOverLimitsException();
        }
        return cells;
    }

    private static bool IsFormulaRange(string? type) =>
        string.Equals(type, "array", StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, "dataTable", StringComparison.OrdinalIgnoreCase);

    /// <summary>Las celdas de un rango como «A1:C10» o «$A$1:$ZZ$100000»; una sola celda es 1.</summary>
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
        var span = reference.AsSpan().Trim();
        long column = 0;
        var index = 0;
        if (index < span.Length && span[index] == '$') index++;
        for (; index < span.Length && char.IsAsciiLetter(span[index]); index++)
            column = Math.Min(column * 26 + (char.ToUpperInvariant(span[index]) - 'A' + 1), 1_000_000);
        if (index < span.Length && span[index] == '$') index++;
        var row = long.TryParse(span[index..], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : 1;
        return (column, Math.Min(row, 10_000_000));
    }

    private static IEnumerable<ZipArchiveEntry> FindEntries(ZipArchive zip, string normalizedPath)
    {
        foreach (var entry in zip.Entries)
        {
            if (string.Equals(NormalizePath(entry.FullName), normalizedPath, StringComparison.OrdinalIgnoreCase))
                yield return entry;
        }
    }

    private static bool IsExternal(XmlReader reader) =>
        string.Equals(reader.GetAttribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase);

    private static void AddResolvedPaths(HashSet<string> targetSet, string baseDirectory, string target)
    {
        var resolved = ResolveTarget(baseDirectory, target);
        if (resolved.Length > 0) targetSet.Add(resolved);
        var unescaped = ResolveTarget(baseDirectory, Uri.UnescapeDataString(target));
        if (unescaped.Length > 0) targetSet.Add(unescaped);
    }

    private static string ResolveTarget(string baseDirectory, string target)
    {
        var normalizedTarget = target.Replace('\\', '/');
        if (normalizedTarget.StartsWith('/')) return NormalizePath(normalizedTarget);
        return string.IsNullOrEmpty(baseDirectory)
            ? NormalizePath(normalizedTarget)
            : NormalizePath($"{baseDirectory}/{normalizedTarget}");
    }

    private static string DirectoryOf(string normalizedPath)
    {
        var slash = normalizedPath.LastIndexOf('/');
        return slash < 0 ? string.Empty : normalizedPath[..slash];
    }

    private static string RelsPathOf(string normalizedPath)
    {
        var slash = normalizedPath.LastIndexOf('/');
        return slash < 0
            ? $"_rels/{normalizedPath}.rels"
            : $"{normalizedPath[..slash]}/_rels/{normalizedPath[(slash + 1)..]}.rels";
    }

    private static string NormalizePath(string path)
    {
        var segments = new List<string>();
        foreach (var segment in path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".") continue;
            if (segment == "..")
            {
                if (segments.Count > 0) segments.RemoveAt(segments.Count - 1);
            }
            else
            {
                segments.Add(segment);
            }
        }
        return string.Join('/', segments);
    }

    private sealed record SheetParts(HashSet<string> WorkbookSheets, Dictionary<string, bool> Overrides, HashSet<string> Extensions)
    {
        public bool Contains(string normalizedName) =>
            WorkbookSheets.Contains(normalizedName)
            || (Overrides.TryGetValue(normalizedName, out var isSheet)
                ? isSheet
                : Extensions.Contains(Path.GetExtension(normalizedName).TrimStart('.'))
                    || (normalizedName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase)
                        && normalizedName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)));
    }

    private sealed class Budget(long remaining)
    {
        public void Spend(int bytes)
        {
            remaining -= bytes;
            if (remaining < 0) throw new SpreadsheetOverLimitsException();
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

        public override int Read(Span<byte> buffer)
        {
            var read = inner.Read(buffer);
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
}
