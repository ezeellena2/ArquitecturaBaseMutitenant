using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Verifica código y cuenta, conserva intentos y auditoría dentro del límite del llamador.</summary>
internal sealed class LoginCodeVerificationFlow(
    LoginCodeVerifier verifier,
    ILoginCodeRepository codes,
    IUserLookup userLookup,
    IUserRepository users,
    ILoginAuditRepository audits,
    ISignInService signIn,
    TimeProvider timeProvider)
{
    internal async Task<Result<Guid>> VerifyAsync(
        VerifyLoginCodeRequest request, CancellationToken cancellationToken)
    {
        var destination = LoginCodeDestination.ForEmail(request.Email!);
        await codes.LockDestinationAsync(destination, cancellationToken);
        var userId = await userLookup.FindVerifiedUserIdAsync(
            LoginMethodType.Email, destination.Value, cancellationToken);
        if (userId is { } id && await signIn.IsLockedOutAsync(id, cancellationToken))
            return Fail(userId, AccountErrors.LockedOut);

        var verified = await verifier.VerifyAsync(destination, LoginCodePurpose.Login,
            requestedByUserId: null, request.Code!, cancellationToken);
        if (verified.IsFailure)
        {
            if (userId is { } existingUserId)
                await signIn.RegisterFailedAttemptAsync(existingUserId, cancellationToken);
            return Fail(userId, verified.Error);
        }

        if (userId is null) return Fail(null, LoginCodeErrors.Invalid(attemptsLeft: null));

        var account = await users.GetByIdAsync(userId.Value, cancellationToken);
        if (account is null || account.Status is UserStatus.Suspended or UserStatus.Deleted)
            return Fail(userId, AccountErrors.Suspended);
        if (account.Status == UserStatus.PendingDeletion)
            return Fail(userId, AccountErrors.PendingDeletion);

        await signIn.ResetFailedAttemptsAsync(userId.Value, cancellationToken);
        audits.Add(LoginAudit.Success(userId.Value, LoginAuditMethod.Code,
            timeProvider.GetUtcNow().UtcDateTime));
        return userId.Value;
    }

    private Error Fail(Guid? userId, Error error)
    {
        audits.Add(LoginAudit.Failure(userId, LoginAuditMethod.Code, error,
            timeProvider.GetUtcNow().UtcDateTime));
        return error;
    }
}
