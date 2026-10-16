using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>
/// Segundo factor del ingreso de un operador (E9, ADR 0043 y 0044): consulta el desafío pendiente del navegador, prueba
/// el autenticador o un código de recuperación y completa el alta. La sesión de Plataforma nace recién al terminar. Las
/// escrituras devuelven la referencia de su vínculo durable para la idempotencia.
/// </summary>
public interface IPlatformFactorService
{
    Task<Result<PlatformFactorChallengeResponse>> GetAsync(CancellationToken cancellationToken);

    Task<Result<IdempotentReplayResponse<VerifyPlatformFactorResponse>>> VerifyAsync(VerifyPlatformFactorRequest request,
        CancellationToken cancellationToken);

    Task<Result<IdempotentReplayResponse<CompletePlatformFactorResponse>>> CompleteAsync(CompletePlatformFactorRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// El protocolo durable del segundo factor (E9): antes de reservar, autoriza con el desafío del navegador o con la sesión
/// del token y busca su vínculo; el replay revalida cuenta, sello, acceso y prueba antes de reconstruir la respuesta.
/// </summary>
public interface IPlatformFactorDurableService
{
    Task<Result<IdempotentReference?>> ValidateFactorAsync(IdempotentRequest request, CancellationToken cancellationToken);

    Task<Result<IdempotentReference?>> ValidateReauthenticationAsync(IdempotentRequest request,
        CancellationToken cancellationToken);

    Task<Result<IdempotentReplayResponse>> ReplayAsync(IdempotentReference reference, CancellationToken cancellationToken);
}

/// <summary>
/// La sesión de Plataforma como evidencia (E9): el authorize y el refresh del operador la exigen, y una acción sensible
/// exige además su prueba reciente, sin caché, y la deja pedida para la transacción que escribe.
/// </summary>
public interface IPlatformSessionService
{
    /// <summary>La sesión del navegador con el segundo factor completo y el mismo sello; si no, SessionEnded.</summary>
    Task<Result<Guid>> FindValidSessionAsync(Guid userId, string browserSessionId, CancellationToken cancellationToken);

    /// <summary>La sesión del token sigue vigente para su cuenta; si no, SessionEnded.</summary>
    Task<Result> CheckSessionAsync(Guid userId, Guid platformSessionId, CancellationToken cancellationToken);

    Task<Result> RequireRecentProofAsync(CancellationToken cancellationToken);
}
