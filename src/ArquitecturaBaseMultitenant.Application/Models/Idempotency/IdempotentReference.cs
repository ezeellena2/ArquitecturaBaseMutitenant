

namespace ArquitecturaBaseMultitenant.Application.Models.Idempotency;

public sealed record IdempotentReference(string Kind, Guid Id, int StatusCode, string? Location);
