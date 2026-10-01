using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>
/// Reúne los errores de verificación y los límites de envío de códigos. Sus fábricas agregan los intentos
/// restantes o los segundos de espera que necesita mostrar el front.
/// </summary>
public static class LoginCodeErrors
{
    public const string InvalidCode = "Auth.LoginCode.Invalid";
    public const string ExpiredCode = "Auth.LoginCode.Expired";
    public const string AlreadyUsedCode = "Auth.LoginCode.AlreadyUsed";
    public const string TooManyAttemptsCode = "Auth.LoginCode.TooManyAttempts";
    public const string ResendTooSoonCode = "Auth.LoginCode.ResendTooSoon";
    public const string TooManyRequestsCode = "Auth.LoginCode.TooManyRequests";

    public const string AttemptsLeftKey = "attemptsLeft";
    public const string RetryAfterKey = "retryAfter";

    public static readonly Error Expired = Error.Validation(ExpiredCode, "The code has expired.");

    public static readonly Error AlreadyUsed = Error.Validation(AlreadyUsedCode, "The code has already been used.");

    public static readonly Error TooManyAttempts = Error.Validation(TooManyAttemptsCode, "Too many failed attempts for this code.");

    /// <summary>Código incorrecto o inexistente. Sin <paramref name="attemptsLeft"/> cuando no hay un código activo.</summary>
    public static Error Invalid(int? attemptsLeft) =>
        Error.Validation(
            InvalidCode,
            "The code is not valid.",
            attemptsLeft is null ? null : new Dictionary<string, object?> { [AttemptsLeftKey] = attemptsLeft });

    /// <summary>Informa cuánto falta para reenviar un código; el front usa ese tiempo en la cuenta regresiva.</summary>
    public static Error ResendTooSoon(int retryAfterSeconds) =>
        Error.TooManyRequests(
            ResendTooSoonCode,
            "A new code can't be requested yet.",
            new Dictionary<string, object?> { [RetryAfterKey] = retryAfterSeconds });

    /// <summary>Informa la espera al superar el límite de pedidos de códigos dentro de la ventana de envío.</summary>
    public static Error TooManyRequests(int retryAfterSeconds) =>
        Error.TooManyRequests(
            TooManyRequestsCode,
            "Too many codes were requested.",
            new Dictionary<string, object?> { [RetryAfterKey] = retryAfterSeconds });
}
