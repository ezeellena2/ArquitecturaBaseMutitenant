using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>
/// Una acción sensible de Plataforma pide una prueba reciente del segundo factor (E9, 10 minutos). El front abre
/// «Confirmá que sos vos» y reenvía la misma operación; no es un permiso.
/// </summary>
public static class PlatformReauthenticationErrors
{
    public const string RequiredCode = "Auth.Platform.ReauthenticationRequired";

    public static readonly Error Required = Error.Forbidden(RequiredCode,
        "A recent second-factor confirmation is required for this action.");

    /// <summary>
    /// La sesión de Plataforma del token terminó entre la autenticación y la confirmación (se cerraron las sesiones, se
    /// quitó el operador o se restableció su factor): falta sesión, como cualquier 401 de la API.
    /// </summary>
    public static readonly Error SessionEnded = Error.Unauthorized("Http.Unauthorized", "The platform session ended.");
}
