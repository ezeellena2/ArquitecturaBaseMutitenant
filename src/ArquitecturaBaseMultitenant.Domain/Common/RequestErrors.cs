using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Common;

public static class RequestErrors
{
    public const string IdempotencyKeyReusedCode = "Request.IdempotencyKeyReused";
    public static readonly Error IdempotencyKeyReused = Error.Validation(IdempotencyKeyReusedCode, "The key belongs to another request.");
}
