using ArquitecturaBaseMultitenant.Application.Models.Idempotency;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Idempotency;

public interface IIdempotentReferencedResult
{
    IdempotentReference Reference { get; }
}
