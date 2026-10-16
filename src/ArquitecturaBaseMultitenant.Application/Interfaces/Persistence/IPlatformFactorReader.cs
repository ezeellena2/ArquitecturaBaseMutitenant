using ArquitecturaBaseMultitenant.Application.Models.Auth;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Lecturas del segundo factor (E9) sin tracking: el desafío que consulta el navegador y la sesión de Plataforma.</summary>
public interface IPlatformFactorReader
{
    Task<PlatformFactorChallengeRow?> FindChallengeAsync(Guid challengeId, CancellationToken cancellationToken);

    Task<bool> HasAuthenticatorAsync(Guid userId, CancellationToken cancellationToken);

    Task<PlatformSessionRow?> FindSessionAsync(Guid platformSessionId, CancellationToken cancellationToken);

    Task<PlatformSessionRow?> FindSessionByHashAsync(Guid userId, string sessionIdHash, CancellationToken cancellationToken);

    Task<PlatformFactorOperationRow?> FindOperationAsync(Guid idempotencyKey, CancellationToken cancellationToken);

    Task<PlatformFactorOperationRow?> FindOperationByIdAsync(Guid operationId, CancellationToken cancellationToken);
}
