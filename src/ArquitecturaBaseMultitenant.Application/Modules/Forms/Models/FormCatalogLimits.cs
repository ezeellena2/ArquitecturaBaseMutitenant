using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Domain.Forms;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

/// <summary>
/// Los topes técnicos de los catálogos (E12) que publica el contrato y validan los servicios. Los de producto salen de la spec
/// (§3, §4 y C3); los de las listas cerradas acotan lo que crece sin paginar (GrowingCollectionContractTests).
/// </summary>
public static class FormCatalogLimits
{
    /// <summary>Opciones de un catálogo a mano, entre activas y dadas de baja (§3).</summary>
    public const int MaxItems = 10_000;
    /// <summary>Movimientos y opciones por borrar en un guardado (C3 y 129).</summary>
    public const int MaxMoves = 500;
    public const int MaxDeleteItems = 500;
    /// <summary>Condiciones de unos filtros, unidas con «y» (§4 y §4b).</summary>
    public const int MaxConditions = 10;
    /// <summary>Valores de una condición: ids de roles o de empresas, o textos de etiqueta.</summary>
    public const int MaxConditionValues = 50;
    /// <summary>El largo de un valor de una condición: un texto que busca en el nombre o una etiqueta.</summary>
    public const int MaxConditionValueLength = 100;
    /// <summary>Los roles o las empresas con nombre que trae el detalle: a lo sumo uno por valor de cada condición.</summary>
    public const int MaxFilterReferences = MaxConditions * MaxConditionValues;
    /// <summary>Columnas y operadores de una fuente: catálogos cerrados del código.</summary>
    public const int MaxSourceColumns = 16;
    public const int MaxColumnOperators = 8;
    /// <summary>Los nombres de empresas y de roles que acompañan a una persona de ejemplo.</summary>
    public const int MaxSampleMemberNames = 50;
    /// <summary>Campos activos de un catálogo a mano: los quitados no cuentan (E13).</summary>
    public const int MaxFields = FormCatalogField.MaxActive;
    /// <summary>
    /// Campos guardados de un catálogo, entre activos y quitados, que se conservan con sus valores y vienen en el detail (E13):
    /// cinco veces el tope de activos, así un catálogo no crece sin fin.
    /// </summary>
    public const int MaxStoredFields = FormCatalogField.MaxStored;
    /// <summary>Los lugares de un título o de una descripción en WhatsApp: texto, código, valor, detalle y cada campo activo (E13).</summary>
    public const int MaxPresentationPlaces = 4 + MaxFields;
    /// <summary>Las opciones que nombra el filtro <c>ids</c> del listado de opciones, para mostrar los valores de una fuente (E13).</summary>
    public const int MaxItemIds = 100;
    /// <summary>Las filas que trae un cambio de tipo o de regla que no entra, en orden de posición (E13).</summary>
    public const int MaxInvalidRows = 50;
    /// <summary>Los ejemplos de las opciones que no entran en WhatsApp (E13).</summary>
    public const int PresentationExamples = 3;
    /// <summary>Opciones de la vista previa: con más, avisa <c>exceedsLimit</c> (§4).</summary>
    public const int PreviewOptions = 200;
    /// <summary>
    /// Los topes del archivo de importar (C8): 10 MiB, 64 columnas, 10.000 filas de datos y 640.128 celdas de trabajo, contando
    /// las vacías con formato y las fórmulas matriciales; un XLSX, hasta 100 MiB descomprimido con sus estilos y textos compartidos.
    /// </summary>
    public const int MaxFileBytes = SpreadsheetLimits.MaxFileBytes;
    public const int MaxFileColumns = 64;
    public const int MaxFileRows = 10_000;
    public const int MaxFileCells = 640_128;
    public const long MaxFileUncompressedBytes = 100L * 1024 * 1024;
    /// <summary>Las partes de un XLSX: uno común trae unas veinte; miles de partes chicas solo cargan la lectura.</summary>
    public const int MaxFileEntries = 1_000;
}
