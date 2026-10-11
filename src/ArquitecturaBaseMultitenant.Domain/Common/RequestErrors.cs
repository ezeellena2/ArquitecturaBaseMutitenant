using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Common;

public static class RequestErrors
{
    public const string InProgressCode = "Request.InProgress";
    public static readonly Error InProgress = Error.Conflict(InProgressCode, "The request is still being processed.");
    public const string IdempotencyKeyReusedCode = "Request.IdempotencyKeyReused";
    public static readonly Error IdempotencyKeyReused = Error.Validation(IdempotencyKeyReusedCode, "The key belongs to another request.");
}
