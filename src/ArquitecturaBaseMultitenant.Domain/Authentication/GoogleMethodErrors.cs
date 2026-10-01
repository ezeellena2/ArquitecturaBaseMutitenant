using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>
/// Define el conflicto que impide vincular un identificador de Google asociado a otra cuenta. Permite
/// informar el rechazo sin trasladar la regla a la API.
/// </summary>
public static class GoogleMethodErrors
{
    public static readonly Error AlreadyUsed = Error.Conflict("Identity.Google.AlreadyUsed", "Google is associated with another account.");
}
