namespace ArquitecturaBaseMultitenant.Domain.Forms;

/// <summary>
/// El tipo del valor de cada opción, fijo desde el alta; un catálogo con filtros es siempre <see cref="Text"/> (C6). Los números
/// se llaman como los de las acciones del asistente (CA1720): <see cref="WholeNumber"/> es el Integer de la spec y
/// <see cref="DecimalNumber"/>, su Decimal.
/// </summary>
public enum FormCatalogValueType
{
    Text,
    WholeNumber,
    DecimalNumber,
    Date,
}
