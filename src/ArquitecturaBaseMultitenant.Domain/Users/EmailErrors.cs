using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Users;

/// <summary>
/// Define el error que se devuelve cuando un correo no cumple el formato admitido. El mismo código
/// identifica la validación y permite traducir su mensaje.
/// </summary>
public static class EmailErrors
{
    public const string InvalidCode = "Users.Email.Invalid";

    public static readonly Error Invalid = Error.Validation(InvalidCode, "The email address is not valid.");
}
