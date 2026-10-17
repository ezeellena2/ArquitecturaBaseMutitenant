namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;

/// <summary>
/// Lee y escribe la planilla de contactos (.xlsx con ClosedXML, o .csv). Solo traslada celdas: qué significa cada columna lo
/// decide el caso de uso.
/// </summary>
public interface IWhatsAppContactSpreadsheet
{
    /// <summary>El ejemplo para descargar: una hoja con su nombre, los encabezados y una fila de muestra.</summary>
    byte[] WriteSample(string sheetName, IReadOnlyList<string> headers, IReadOnlyList<string> example);

    /// <summary>
    /// La primera hoja (o el .csv): encabezados y filas con su número en el archivo (el encabezado es la fila 1). Lee como mucho
    /// <paramref name="maxRows"/> + 1 filas de datos; null si el archivo no es una planilla que se pueda leer.
    /// </summary>
    Task<WhatsAppSpreadsheet?> ReadAsync(string? fileName, Stream content, int maxRows, CancellationToken ct);
}

public sealed record WhatsAppSpreadsheet(IReadOnlyList<string> Headers, IReadOnlyList<WhatsAppSpreadsheetRow> Rows);

public sealed record WhatsAppSpreadsheetRow(int Number, IReadOnlyList<string?> Cells)
{
    public override string ToString() => nameof(WhatsAppSpreadsheetRow);
}
