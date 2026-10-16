using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Settings;

/// <summary>
/// Una cuota compartida rechazó el pedido (E9). Informa los segundos enteros que faltan para la próxima ventana; la
/// API los repite en Retry-After. El webhook de WhatsApp conserva su propio código.
/// </summary>
public static class RateLimitErrors
{
    public const string ExceededCode = "Security.RateLimit.Exceeded";

    public static Error Exceeded(int retryAfterSeconds) => Error.TooManyRequests(ExceededCode,
        "Too many requests. Wait for the window to restart.",
        new Dictionary<string, object?> { [LoginCodeErrors.RetryAfterKey] = retryAfterSeconds });
}
