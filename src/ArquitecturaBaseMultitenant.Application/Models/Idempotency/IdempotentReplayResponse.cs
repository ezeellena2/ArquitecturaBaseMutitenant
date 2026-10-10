

namespace ArquitecturaBaseMultitenant.Application.Models.Idempotency;

public abstract record IdempotentReplayResponse(IdempotentReference Reference);

public sealed record IdempotentReplayResponse<T>(IdempotentReference Reference, T Value) : IdempotentReplayResponse(Reference);
