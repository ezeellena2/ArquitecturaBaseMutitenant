namespace ArquitecturaBaseMultitenant.Domain.Forms;

/// <summary>
/// Una opción que no admite el tipo o la regla nueva de un campo (E13), en <c>invalidRows</c> de
/// <see cref="FormCatalogErrors.FieldTypeNotConvertibleCode"/> y <see cref="FormCatalogErrors.FieldValuesIncompatibleCode"/>:
/// su posición, su identidad, su texto y el motivo (<see cref="Required"/>, <see cref="Range"/>, <see cref="Length"/> o
/// <see cref="Invalid"/>), cuyo texto está en el front.
/// </summary>
public sealed record FormCatalogInvalidRow(int Position, Guid ItemId, string Text, string Reason)
{
    public const string Required = "required";
    public const string Range = "range";
    public const string Length = "length";
    public const string Invalid = "invalid";
}
