using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;

public interface IAssistantDurableService
{
    Task<Result<IdempotentReference?>> ValidateAsync(IdempotentRequest request, string? quickKey, CancellationToken ct);
    Task<Result<IdempotentReplayResponse>> ReplayAsync(IdempotentReference reference, CancellationToken ct);
}
