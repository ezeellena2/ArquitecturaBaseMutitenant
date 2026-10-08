using ArquitecturaBaseMultitenant.Domain.Results;
namespace ArquitecturaBaseMultitenant.Api.Idempotency;

/// <summary>Revalidación explícita y neutral de una respuesta confirmada, antes de repetirla.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotencyReplayAttribute(Type verifierType) : Attribute
{
    public Type VerifierType { get; } = verifierType;
}
public interface IIdempotencyReplayVerifier
{
    Task<Result> ValidateAsync(HttpRequest request, int originalStatusCode, string? originalBody, CancellationToken ct);
}
