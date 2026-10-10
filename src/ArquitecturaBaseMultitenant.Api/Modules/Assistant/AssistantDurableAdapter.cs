using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Idempotency;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant;

public sealed class AssistantDurableAdapter(IAssistantDurableService service, IHttpContextAccessor http) : IIdempotentRequestVerifier
{
    public Task<Result<IdempotentReference?>> ValidateAsync(IdempotentRequest request, CancellationToken cancellationToken) =>
        service.ValidateAsync(request, http.HttpContext?.Request.RouteValues["key"] as string, cancellationToken);
}

internal sealed class AssistantReplayAdapter(IAssistantDurableService service, string kind) : IIdempotentReplayResolver
{
    public string Kind { get; } = kind;
    public Task<Result<IdempotentReplayResponse>> ResolveAsync(IdempotentReference reference, CancellationToken cancellationToken) =>
        service.ReplayAsync(reference, cancellationToken);
}
