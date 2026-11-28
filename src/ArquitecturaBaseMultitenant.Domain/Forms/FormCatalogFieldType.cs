namespace ArquitecturaBaseMultitenant.Domain.Forms;

/// <summary>
/// El tipo de un campo de las opciones de un catálogo a mano (E13): texto, número entero o con decimales, fecha, sí/no o «De un
/// catálogo», que toma sus valores de las opciones de otro catálogo a mano activo de la misma organización.
/// </summary>
public enum FormCatalogFieldType
{
    Text,
    WholeNumber,
    DecimalNumber,
    Date,
    YesNo,
    Catalog,
}
