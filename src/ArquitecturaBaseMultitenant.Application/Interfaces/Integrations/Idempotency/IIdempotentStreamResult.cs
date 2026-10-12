namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Idempotency;

/// <summary>An admitted reader has published its durable reference before its first frame.</summary>
public interface IIdempotentStreamResult : IIdempotentReferencedResult { }
