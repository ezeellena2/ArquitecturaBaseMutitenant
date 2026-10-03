using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Application.Services.Legal;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Verifica código y cuenta, conserva intentos y auditoría dentro del límite del llamador.</summary>
internal sealed class LoginCodeVerificationFlow(
    LoginCodeVerifier verifier,
    LoginCodeAttemptCoordinator attempts,
    LoginAccountGuard accounts,
    ILoginMethodReader loginMethods,
    LoginMethodAvailability availability,
    AccountDeletionCancelIssuer cancellation,
    IConnectService connect)
{
    internal async Task<Result<Guid>> VerifyAsync(
        VerifyLoginCodeRequest request, CancellationToken cancellationToken)
    {
        var destination = LoginCodeDestination.ForEmail(request.Email!);
        var userId = await accounts.LockAsync(destination, cancellationToken);
        await attempts.LockDestinationAsync(destination, cancellationToken);
        if (!await accounts.StillOwnsAsync(userId, destination, cancellationToken))
            return Fail(userId, LoginCodeErrors.Invalid(attemptsLeft: null));
        var hadActiveCode = await attempts.HasActiveCodeAsync(destination, cancellationToken);
        var verified = await verifier.VerifyAsync(destination, LoginCodePurpose.Login,
            requestedByUserId: null, request.Code!, cancellationToken);
        if (verified.IsFailure)
        {
            if (userId is { } existingUserId && hadActiveCode && IsFailedCodeAttempt(verified.Error))
            {
                await attempts.RegisterFailedAttemptAsync(existingUserId, cancellationToken);
                if (await attempts.IsLockedOutAsync(existingUserId, cancellationToken))
                    return Fail(userId, AccountErrors.LockedOut);
            }
            return Fail(userId, verified.Error);
        }

        if (userId is null) return Fail(null, LoginCodeErrors.Invalid(attemptsLeft: null));
        if (await attempts.IsLockedOutAsync(userId.Value, cancellationToken))
            return Fail(userId, AccountErrors.LockedOut);

        var account = await accounts.ReadAsync(userId.Value, cancellationToken);
        if (account is null || account.Status is UserStatus.Suspended or UserStatus.Deleted)
            return Fail(userId, AccountErrors.Suspended);
        if (account.Status == UserStatus.PendingDeletion)
            return Fail(userId, cancellation.Issue(account, null, request.ReturnUrl!));

        var emailMethod = (await loginMethods.ListByUserIdAsync(userId.Value, cancellationToken))
            .SingleOrDefault(method => method.Type == LoginMethodType.Email && method.Value == destination.Value);
        if (emailMethod is null || !await availability.IsAvailableAsync(userId.Value, emailMethod, cancellationToken))
            return Fail(userId, LoginCodeErrors.Invalid(attemptsLeft: null));

        if (request.ReturnUrl != ReturnUrls.InvitationPath)
        {
            if (!ReturnUrls.TryReadAccessSelection(request.ReturnUrl, out var selection))
                throw new InvalidOperationException("A validated return URL became invalid.");
            if (selection.Access == Access.Business)
            {
                var selected = await connect.GetActiveUserAsync(userId.Value, selection.Access,
                    selection.TenantId, cancellationToken);
                if (selected.IsFailure) return Fail(userId, selected.Error);
            }
        }

        await attempts.ResetFailedAttemptsAsync(userId.Value, cancellationToken);
        attempts.RecordSuccess(userId.Value);
        return userId.Value;
    }

    private static bool IsFailedCodeAttempt(Error error) =>
        error.Code == LoginCodeErrors.TooManyAttemptsCode
        || (error.Code == LoginCodeErrors.InvalidCode
            && error.Metadata?.ContainsKey(LoginCodeErrors.AttemptsLeftKey) == true);

    private Error Fail(Guid? userId, Error error)
    {
        return attempts.RecordFailure(userId, error);
    }
}
