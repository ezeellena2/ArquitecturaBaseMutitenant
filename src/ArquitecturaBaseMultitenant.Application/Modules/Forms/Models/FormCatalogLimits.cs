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
    /// <summary>Los roles o las empresas con nombre que trae el detalle: a lo sumo uno por valor de cada condición.</summary>
    public const int MaxFilterReferences = MaxConditions * MaxConditionValues;
    /// <summary>Columnas y operadores de una fuente: catálogos cerrados del código.</summary>
    public const int MaxSourceColumns = 16;
    public const int MaxColumnOperators = 8;
    /// <summary>Los nombres de empresas y de roles que acompañan a una persona de ejemplo.</summary>
    public const int MaxSampleMemberNames = 50;
    /// <summary>Opciones de la vista previa: con más, avisa <c>exceedsLimit</c> (§4).</summary>
    public const int PreviewOptions = 200;
}
