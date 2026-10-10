using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Idempotency;

namespace ArquitecturaBaseMultitenant.Api.Idempotency;

[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotencyDurableAttribute(Type verifierType) : Attribute
{
    public Type VerifierType { get; } = typeof(IIdempotentRequestVerifier).IsAssignableFrom(verifierType)
        ? verifierType : throw new ArgumentException("A durable request verifier is required.", nameof(verifierType));
}
