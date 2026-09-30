using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
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
    IConnectService connect,
    TimeProvider timeProvider)
{
    internal async Task<Result<Guid>> VerifyAsync(
        VerifyLoginCodeRequest request, CancellationToken cancellationToken)
    {
        var destination = LoginCodeDestination.ForEmail(request.Email!);
        await codes.LockDestinationAsync(destination, cancellationToken);
        var userId = await userLookup.FindVerifiedUserIdAsync(
            LoginMethodType.Email, destination.Value, cancellationToken);
        var currentCode = await codes.GetLatestAsync(destination, LoginCodePurpose.Login,
            requestedByUserId: null, cancellationToken);
        var hadActiveCode = currentCode?.IsActive(timeProvider.GetUtcNow().UtcDateTime) == true;
        var verified = await verifier.VerifyAsync(destination, LoginCodePurpose.Login,
            requestedByUserId: null, request.Code!, cancellationToken);
        if (verified.IsFailure)
        {
            if (userId is { } existingUserId && hadActiveCode && IsFailedCodeAttempt(verified.Error))
            {
                await signIn.RegisterFailedAttemptAsync(existingUserId, cancellationToken);
                if (await signIn.IsLockedOutAsync(existingUserId, cancellationToken))
                    return Fail(userId, AccountErrors.LockedOut);
            }
            return Fail(userId, verified.Error);
        }

        if (userId is null) return Fail(null, LoginCodeErrors.Invalid(attemptsLeft: null));
        if (await signIn.IsLockedOutAsync(userId.Value, cancellationToken))
            return Fail(userId, AccountErrors.LockedOut);

        var account = await users.GetByIdAsync(userId.Value, cancellationToken);
        if (account is null || account.Status is UserStatus.Suspended or UserStatus.Deleted)
            return Fail(userId, AccountErrors.Suspended);
        if (account.Status == UserStatus.PendingDeletion)
            return Fail(userId, AccountErrors.PendingDeletion);

        if (!ReturnUrls.TryReadAccessSelection(request.ReturnUrl, out var selection))
            throw new InvalidOperationException("A validated return URL became invalid.");
        if (selection.Access == Access.Business)
        {
            var selected = await connect.GetActiveUserAsync(userId.Value, selection.Access,
                selection.TenantId, cancellationToken);
            if (selected.IsFailure) return Fail(userId, selected.Error);
        }

        await signIn.ResetFailedAttemptsAsync(userId.Value, cancellationToken);
        audits.Add(LoginAudit.Success(userId.Value, LoginAuditMethod.Code,
            timeProvider.GetUtcNow().UtcDateTime));
        return userId.Value;
    }

    private static bool IsFailedCodeAttempt(Error error) =>
        error.Code == LoginCodeErrors.TooManyAttemptsCode
        || (error.Code == LoginCodeErrors.InvalidCode
            && error.Metadata?.ContainsKey(LoginCodeErrors.AttemptsLeftKey) == true);

    private Error Fail(Guid? userId, Error error)
    {
        audits.Add(LoginAudit.Failure(userId, LoginAuditMethod.Code, error,
            timeProvider.GetUtcNow().UtcDateTime));
        return error;
    }
}
