using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Idempotency;

public interface IIdempotentReplayResolver
{
    string Kind { get; }
    Task<Result<IdempotentReplayResponse>> ResolveAsync(IdempotentReference reference, CancellationToken cancellationToken);
}
