using ArquitecturaBaseMultitenant.Application.Models.Identity;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;

/// <summary>Operaciones técnicas de Identity; la cuenta y los métodos se consultan por puertos separados.</summary>
public interface ISignInService
{
    Task<bool> IsLockedOutAsync(Guid userId, CancellationToken cancellationToken);

    Task RegisterFailedAttemptAsync(Guid userId, CancellationToken cancellationToken);

    Task ResetFailedAttemptsAsync(Guid userId, CancellationToken cancellationToken);

    Task RevokeSessionsAsync(Guid userId, CancellationToken cancellationToken);

    Task SignInAsync(Guid userId, CancellationToken cancellationToken);

    Task<ExternalLogin?> GetExternalLoginAsync(CancellationToken cancellationToken);

    Task SignOutExternalAsync(CancellationToken cancellationToken);
}
