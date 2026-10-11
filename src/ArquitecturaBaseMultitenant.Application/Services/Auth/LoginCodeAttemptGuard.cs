using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Coordina lock, intentos fallidos y auditoría del código de ingreso.</summary>
internal sealed class LoginCodeAttemptGuard(ILoginCodeRepository codes, ISignInService signIn,
    ILoginAuditRepository audits, TimeProvider timeProvider, LoginDestinationResolver destinations)
{
    internal Task LockDestinationAsync(LoginCodeDestination destination, CancellationToken cancellationToken) =>
        codes.LockDestinationAsync(destination, cancellationToken);

    internal Task<bool> IsLockedOutAsync(Guid userId, CancellationToken cancellationToken) =>
        signIn.IsLockedOutAsync(userId, cancellationToken);

    internal Task ResetFailedAttemptsAsync(Guid userId, CancellationToken cancellationToken) =>
        signIn.ResetFailedAttemptsAsync(userId, cancellationToken);

    internal Error RecordFailure(LoginCodeDestination destination, Guid? userId, Error error)
    {
        audits.Add(LoginAudit.Failure(userId, destinations.AuditMethod(destination), error, timeProvider.GetUtcNow().UtcDateTime));
        return error;
    }

    internal void RecordSuccess(LoginCodeDestination destination, Guid userId) => audits.Add(LoginAudit.Success(userId, destinations.AuditMethod(destination),
        timeProvider.GetUtcNow().UtcDateTime));
}
