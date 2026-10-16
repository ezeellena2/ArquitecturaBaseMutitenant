using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Api.RateLimiting;

/// <summary>
/// Metadata de una ruta que conserva su propio error al llegar a una cuota, como el webhook de un módulo. Sin ella, el
/// rechazo es <c>Security.RateLimit.Exceeded</c>.
/// </summary>
public interface IRateLimitRejection
{
    Error Rejected(int retryAfterSeconds);
}
