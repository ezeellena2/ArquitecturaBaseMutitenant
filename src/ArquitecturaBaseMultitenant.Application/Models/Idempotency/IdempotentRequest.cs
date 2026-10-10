namespace ArquitecturaBaseMultitenant.Application.Models.Idempotency;

public sealed record IdempotentRequest(Guid Key, string BodyHash, string Route, string? Origin = null);
