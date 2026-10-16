using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;

/// <summary>Operaciones técnicas de Identity; la cuenta y los métodos se consultan por puertos separados.</summary>
public interface ISignInService
{
    Task<bool> IsLockedOutAsync(Guid userId, CancellationToken cancellationToken);

    Task RegisterFailedAttemptAsync(Guid userId, CancellationToken cancellationToken);

    Task ResetFailedAttemptsAsync(Guid userId, CancellationToken cancellationToken);

    Task RevokeSessionsAsync(Guid userId, CancellationToken cancellationToken);

    Task SignInAsync(Guid userId, Access access, CancellationToken cancellationToken);

    /// <summary>
    /// La cookie de Plataforma con el identificador de sesión que el segundo factor ya guardó (E9), después del commit.
    /// </summary>
    Task SignInPlatformAsync(Guid userId, string sessionId, DateTime startedAtUtc, CancellationToken cancellationToken);

    /// <summary>El sello de seguridad actual: cambia al cerrar sesiones, quitar el operador o restablecer su factor.</summary>
    Task<string> GetSecurityStampAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Hasta cuándo dura el bloqueo de la cuenta, o null si no está bloqueada.</summary>
    Task<DateTime?> GetLockoutEndUtcAsync(Guid userId, CancellationToken cancellationToken);

    Task<ExternalLogin?> GetExternalLoginAsync(CancellationToken cancellationToken);

    Task SignOutExternalAsync(CancellationToken cancellationToken);
}
