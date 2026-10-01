using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>
/// Centraliza los rechazos del ingreso con Google: fallo del proveedor, correo sin verificar o cuenta sin
/// vínculo. Los servicios devuelven estos errores como Result.
/// </summary>
public static class ExternalLoginErrors
{
    public const string FailedCode = "Auth.ExternalLogin.Failed";
    public const string EmailNotVerifiedCode = "Auth.ExternalLogin.EmailNotVerified";
    public const string AccountNotFoundCode = "Auth.Google.AccountNotFound";

    public static readonly Error Failed = Error.Unauthorized(FailedCode, "Google sign-in could not be completed.");
    public static readonly Error EmailNotVerified = Error.Forbidden(EmailNotVerifiedCode,
        "The Google account does not have a verified email address.");
    public static readonly Error AccountNotFound = Error.Unauthorized(AccountNotFoundCode,
        "No account is linked to this Google login.");
}
