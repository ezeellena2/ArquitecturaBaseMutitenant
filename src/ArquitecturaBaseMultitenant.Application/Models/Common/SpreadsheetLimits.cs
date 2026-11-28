namespace ArquitecturaBaseMultitenant.Application.Models.Common;

/// <summary>
/// Los topes de bytes de una planilla que se importa (B2 de los arreglos de la revisión): el del archivo, que mira el escáner
/// común antes de abrir el libro, y el del cuerpo multipart que lo trae, que Kestrel corta antes de bufferear. El del cuerpo
/// sale del archivo más el margen de las demás partes del formulario, así subir uno sube el otro.
/// </summary>
public static class SpreadsheetLimits
{
    /// <summary>El archivo: 10 MiB, el tope de la importación de catálogos (C8 de la E12) y el de contactos de WhatsApp.</summary>
    public const int MaxFileBytes = 10 * 1024 * 1024;

    /// <summary>Lo que el cuerpo multipart suma al archivo: los encabezados de cada parte y los campos del formulario.</summary>
    public const int MultipartOverheadBytes = 1024 * 1024;

    /// <summary>El cuerpo multipart de una ruta que recibe una planilla.</summary>
    public const long MaxRequestBytes = (long)MaxFileBytes + MultipartOverheadBytes;
}
