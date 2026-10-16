using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>
/// Prepara los cambios del segundo factor de un operador (E9) dentro de la transacción del servicio llamador: el
/// autenticador, los códigos de recuperación, el desafío pendiente, la sesión de Plataforma y el vínculo durable de
/// cada operación con su clave de idempotencia.
/// </summary>
public interface IPlatformFactorRepository
{
    /// <summary>El lock de la cuenta que toman también el primer factor y la gestión de métodos.</summary>
    Task LockAccountAsync(Guid userId, CancellationToken cancellationToken);

    Task<PlatformAuthenticator?> GetAuthenticatorAsync(Guid userId, CancellationToken cancellationToken);

    void Add(PlatformAuthenticator authenticator);

    Task<PlatformRecoveryCode?> GetRecoveryCodeAsync(Guid userId, string codeHash, CancellationToken cancellationToken);

    void Add(IEnumerable<PlatformRecoveryCode> codes);

    /// <summary>Lee el desafío con un lock de fila: dos verificaciones con claves distintas no consumen el mismo código.</summary>
    Task<PlatformFactorChallenge?> GetChallengeForUpdateAsync(Guid challengeId, CancellationToken cancellationToken);

    void Add(PlatformFactorChallenge challenge);

    Task<PlatformSession?> GetSessionAsync(Guid platformSessionId, CancellationToken cancellationToken);

    void Add(PlatformSession session);

    Task<PlatformFactorOperation?> GetOperationAsync(Guid idempotencyKey, CancellationToken cancellationToken);

    void Add(PlatformFactorOperation operation);

    /// <summary>Invalida los desafíos que el operador todavía tenía abiertos.</summary>
    Task InvalidateChallengesAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken);

    /// <summary>Salir de Plataforma: la sesión de ese navegador deja de ser evidencia del segundo factor.</summary>
    Task RevokeSessionAsync(Guid userId, string sessionIdHash, DateTime nowUtc, CancellationToken cancellationToken);

    /// <summary>Revoca todas sus sesiones de Plataforma: ninguna conserva la prueba reciente.</summary>
    Task RevokeSessionsAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken);

    /// <summary>Restablecer: quita la clave y los códigos; el próximo ingreso exige el alta.</summary>
    Task RemoveFactorAsync(Guid userId, CancellationToken cancellationToken);
}
