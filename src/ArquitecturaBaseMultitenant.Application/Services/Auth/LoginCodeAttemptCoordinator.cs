using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Coordina lock, intentos fallidos y auditoría del código de ingreso.</summary>
internal sealed class LoginCodeAttemptCoordinator(ILoginCodeRepository codes, ISignInService signIn,
    ILoginAuditRepository audits, TimeProvider timeProvider)
{
    internal Task LockDestinationAsync(LoginCodeDestination destination, CancellationToken cancellationToken) =>
        codes.LockDestinationAsync(destination, cancellationToken);

    internal async Task<bool> HasActiveCodeAsync(LoginCodeDestination destination,
        CancellationToken cancellationToken)
    {
        var currentCode = await codes.GetLatestAsync(destination, LoginCodePurpose.Login,
            requestedByUserId: null, cancellationToken);
        return currentCode?.IsActive(timeProvider.GetUtcNow().UtcDateTime) == true;
    }

    internal Task RegisterFailedAttemptAsync(Guid userId, CancellationToken cancellationToken) =>
        signIn.RegisterFailedAttemptAsync(userId, cancellationToken);

    internal Task<bool> IsLockedOutAsync(Guid userId, CancellationToken cancellationToken) =>
        signIn.IsLockedOutAsync(userId, cancellationToken);

    internal Task ResetFailedAttemptsAsync(Guid userId, CancellationToken cancellationToken) =>
        signIn.ResetFailedAttemptsAsync(userId, cancellationToken);

    internal Error RecordFailure(Guid? userId, Error error)
    {
        audits.Add(LoginAudit.Failure(userId, LoginAuditMethod.Code, error, timeProvider.GetUtcNow().UtcDateTime));
        return error;
    }

    internal void RecordSuccess(Guid userId) => audits.Add(LoginAudit.Success(userId, LoginAuditMethod.Code,
        timeProvider.GetUtcNow().UtcDateTime));
}
