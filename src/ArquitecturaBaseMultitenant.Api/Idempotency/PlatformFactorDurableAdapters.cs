using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Idempotency;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Api.Idempotency;

/// <summary>Verificar y completar el segundo factor (E9): autoriza con el desafío del navegador antes de reservar.</summary>
public sealed class PlatformFactorDurableAdapter(IPlatformFactorDurableService service) : IIdempotentRequestVerifier
{
    public Task<Result<IdempotentReference?>> ValidateAsync(IdempotentRequest request, CancellationToken cancellationToken) =>
        service.ValidateFactorAsync(request, cancellationToken);
}

/// <summary>«Confirmá que sos vos» (E9): autoriza con la sesión de Plataforma del token antes de reservar.</summary>
public sealed class PlatformReauthenticationDurableAdapter(IPlatformFactorDurableService service) : IIdempotentRequestVerifier
{
    public Task<Result<IdempotentReference?>> ValidateAsync(IdempotentRequest request, CancellationToken cancellationToken) =>
        service.ValidateReauthenticationAsync(request, cancellationToken);
}

/// <summary>El replay de una operación del segundo factor, por el tipo de su referencia.</summary>
internal sealed class PlatformFactorReplayAdapter(IPlatformFactorDurableService service, string kind) : IIdempotentReplayResolver
{
    public string Kind { get; } = kind;

    public Task<Result<IdempotentReplayResponse>> ResolveAsync(IdempotentReference reference,
        CancellationToken cancellationToken) => service.ReplayAsync(reference, cancellationToken);
}
