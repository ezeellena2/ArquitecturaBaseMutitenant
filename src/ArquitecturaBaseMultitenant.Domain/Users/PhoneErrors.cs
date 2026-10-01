using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Users;

/// <summary>
/// Define el error que se devuelve cuando un teléfono no cumple el formato admitido. Permite compartir el
/// mismo código de validación entre las reglas y sus traducciones.
/// </summary>
public static class PhoneErrors
{
    public const string InvalidCode = "Users.Phone.Invalid";

    public static readonly Error Invalid = Error.Validation(InvalidCode, "The phone number is not valid.");
}
