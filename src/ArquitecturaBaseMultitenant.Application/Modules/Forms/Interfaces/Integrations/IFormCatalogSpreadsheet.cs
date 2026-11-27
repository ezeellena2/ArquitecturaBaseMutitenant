namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Integrations;

/// <summary>
/// Lee y escribe la planilla de las opciones de un catálogo (.xlsx con ClosedXML, o .csv en UTF-8 estricto; C8). Solo traslada
/// celdas: qué significa cada columna, la cultura y los errores de cada fila los decide el caso de uso. Application no referencia
/// ClosedXML.
/// </summary>
public interface IFormCatalogSpreadsheet
{
    /// <summary>
    /// Una planilla de una hoja: los encabezados, con su nota si la traen, y las filas que van llegando, cada celda con su tipo y
    /// su formato de Excel (C10).
    /// </summary>
    Task<byte[]> WriteAsync(FormCatalogSheetLayout layout, IAsyncEnumerable<IReadOnlyList<FormCatalogCell>> rows, CancellationToken ct);

    /// <summary>
    /// La primera hoja de un .xlsx, o el .csv, con el encabezado en la fila 1 y sin las filas vacías. Los topes se cuentan antes de
    /// cargar el libro: lo descomprimido, con los estilos y los textos compartidos, y las celdas de todas las hojas, también las
    /// vacías con formato y las de una fórmula matricial. Una celda numérica o de fecha de un XLSX conserva su valor, y una fórmula
    /// cuenta por su valor guardado (C9).
    /// </summary>
    FormCatalogSheetRead Read(string? fileName, byte[] content, FormCatalogFileLimits limits);
}

public sealed record FormCatalogFileLimits(int MaxColumns, int MaxRows, int MaxCells, long MaxUncompressedBytes);

public sealed record FormCatalogSheetLayout(string Name, IReadOnlyList<FormCatalogSheetHeader> Headers);

/// <summary>Un encabezado, con la nota de su celda (si es obligatorio) o sin ella.</summary>
public sealed record FormCatalogSheetHeader(string Text, string? Note);

/// <summary>
/// Una celda: un texto, un número o una fecha. Al escribir, <see cref="Format"/> es el formato de número de Excel del número o de
/// la fecha; al leer, el número o la fecha vienen con su valor, sin la cultura.
/// </summary>
public sealed record FormCatalogCell(string? Text, decimal? Number = null, DateOnly? Date = null, string? Format = null)
{
    public static FormCatalogCell Blank { get; } = new((string?)null);

    public bool IsBlank => Number is null && Date is null && string.IsNullOrWhiteSpace(Text);

    public override string ToString() => nameof(FormCatalogCell);
}

public enum FormCatalogSheetStatus
{
    Read,
    Invalid,
    OverLimits,
}

public sealed record FormCatalogSheetRead(FormCatalogSheetStatus Status, string? SheetName, IReadOnlyList<string> Headers,
    IReadOnlyList<FormCatalogSheetRow> Rows)
{
    public static FormCatalogSheetRead Invalid { get; } = new(FormCatalogSheetStatus.Invalid, null, [], []);
    public static FormCatalogSheetRead OverLimits { get; } = new(FormCatalogSheetStatus.OverLimits, null, [], []);
}

/// <summary>Una fila con su número en el archivo (el encabezado es la 1).</summary>
public sealed record FormCatalogSheetRow(int Number, IReadOnlyList<FormCatalogCell> Cells)
{
    public override string ToString() => nameof(FormCatalogSheetRow);
}
