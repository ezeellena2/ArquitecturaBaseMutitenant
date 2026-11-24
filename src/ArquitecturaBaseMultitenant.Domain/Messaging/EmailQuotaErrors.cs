using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Messaging;

/// <summary>
/// Los topes de correo de la plataforma (ADR 0063 y decisión 256): un código o una invitación por correo que supera su tope
/// de las últimas 24 horas no sale ni se guarda, y la espera va en <c>retryAfter</c>, en segundos.
/// </summary>
public static class EmailQuotaErrors
{
    public const string DailyLimitCode = "Messaging.Email.DailyLimit";
    public const string RetryAfterKey = "retryAfter";

    public static Error DailyLimit(int retryAfterSeconds) => Error.TooManyRequests(DailyLimitCode,
        "The email limit for the last 24 hours was reached.",
        new Dictionary<string, object?> { [RetryAfterKey] = Math.Max(1, retryAfterSeconds) });
}
