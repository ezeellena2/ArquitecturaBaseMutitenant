using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>
/// Centraliza los errores al agregar, verificar, quitar o elegir un método de ingreso. Incluye las reglas
/// que conservan al menos un método utilizable y los errores asociados al campo correo.
/// </summary>
public static class LoginMethodErrors
{
    public const string AlreadyUsedCode = "Identity.LoginMethod.AlreadyUsed";
    public static readonly Error NotFound = Error.NotFound("Identity.LoginMethod.NotFound", "The method was not found.");
    public static readonly Error AlreadyVerified = Error.Conflict("Identity.LoginMethod.AlreadyVerified", "The method is already verified.");
    public static readonly Error LastMethod = Error.Conflict("Identity.LoginMethod.LastMethod", "Keep at least one available verified sign-in method.");
    public static readonly Error LastPersonalMethod = Error.Conflict("Identity.LoginMethod.LastPersonalMethod", "Keep at least one personal sign-in method.");
    public static ValidationError AlreadyUsed => new(AlreadyUsedCode, "The email is already in use.",
        new Dictionary<string, string[]> { ["email"] = [AlreadyUsedCode] });
    public const string NotVerifiedCode = "Auth.LoginMethod.NotVerified";

    public static readonly Error NotVerified = Error.Conflict(NotVerifiedCode, "The login method has not been verified.");
}
