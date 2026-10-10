using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Idempotency;

/// <summary>Authorizes the owner before transport reservation and recovers a committed private binding.</summary>
public interface IIdempotentRequestVerifier
{
    Task<Result<IdempotentReference?>> ValidateAsync(IdempotentRequest request, CancellationToken cancellationToken);
}
