using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>
/// Rechazos del segundo factor de Plataforma (E9): código del autenticador o de recuperación que no sirve, desafío
/// ausente o vencido y paso fuera de secuencia. Los dos primeros van en el campo del código y suman al bloqueo de
/// cuenta existente.
/// </summary>
public static class TwoFactorErrors
{
    public const string InvalidCode = "Auth.TwoFactor.Invalid";
    public const string RecoveryInvalidCode = "Auth.TwoFactor.RecoveryInvalid";
    public const string ChallengeExpiredCode = "Auth.TwoFactor.ChallengeExpired";
    public const string InvalidStateCode = "Auth.TwoFactor.InvalidState";

    /// <summary>El campo del cuerpo que recibe los dos errores de código.</summary>
    public const string CodeField = "code";

    public static readonly Error Invalid = Error.Validation(InvalidCode, "The authenticator code is not valid.");
    public static readonly Error RecoveryInvalid = Error.Validation(RecoveryInvalidCode,
        "The recovery code is not valid or was already used.");
    public static readonly Error ChallengeExpired = Error.Unauthorized(ChallengeExpiredCode,
        "The sign-in challenge is missing, expired or no longer valid.");
    public static readonly Error InvalidState = Error.Conflict(InvalidStateCode,
        "The sign-in challenge is at another step.");
}
