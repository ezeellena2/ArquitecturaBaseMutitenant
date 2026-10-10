

namespace ArquitecturaBaseMultitenant.Application.Models.Idempotency;

public abstract record IdempotentReplayResponse(IdempotentReference Reference)
{
    public abstract object? ResponseValue { get; }
}

public sealed record IdempotentReplayResponse<T>(IdempotentReference Reference, T Value) : IdempotentReplayResponse(Reference)
{
    public override object? ResponseValue => Value;
}
