using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Application.Services.Legal;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Services.Invitations;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Verifica código y cuenta, conserva intentos y auditoría dentro del límite del llamador.</summary>
internal sealed class LoginVerifier(
    LoginCodeAttemptVerifier verifier,
    LoginCodeAttemptGuard attempts,
    LoginAccountGuard accounts,
    ILoginMethodReader loginMethods,
    LoginMethodAvailabilityPolicy availability,
    AccountDeletionCancelIssuer cancellation,
    IConnectService connect,
    LoginInvitationAcceptanceWriter invitations)
{
    internal Result<LoginCodeDestination> Resolve(Email? email,
        PhoneInputRequest? phone, string? channel) => accounts.Resolve(email, phone, channel);

    internal async Task<Result<LoginVerification>> VerifyAsync(
        VerifyLoginCodeRequest request, LoginCodeDestination destination, AccessSelectionRequest? selection,
        LoginInvitationsContext? invitation, CancellationToken cancellationToken)
    {
        if (invitation is not null) await invitations.LockAsync(invitation, cancellationToken);
        var userId = invitation is not null ? invitation.ExpectedOwnerId : await accounts.LockAsync(destination, cancellationToken);
        await attempts.LockDestinationAsync(destination, cancellationToken);
        if (invitation is null && !await accounts.StillOwnsAsync(userId, destination, cancellationToken))
            return Fail(destination, userId, LoginCodeErrors.Invalid(attemptsLeft: null));
        var ownerId = LoginDestinationResolver.Type(destination) == LoginMethodType.Phone ? userId : null;
        var verified = await verifier.VerifyAsync(destination, LoginCodePurpose.Login,
            ownerId, request.Code!, userId, cancellationToken);
        if (verified.IsFailure) return Fail(destination, userId, verified.Error);

        if (invitation is not null)
        {
            var accepted = await invitations.AcceptAsync(invitation, cancellationToken);
            if (accepted.IsFailure) return Fail(destination, userId, accepted.Error);
            await attempts.ResetFailedAttemptsAsync(accepted.Value, cancellationToken);
            attempts.RecordSuccess(destination, accepted.Value);
            return new LoginVerification(accepted.Value, NeedsPlatformFactor: false);
        }
        if (userId is null) return Fail(destination, null, LoginCodeErrors.Invalid(attemptsLeft: null));
        if (await attempts.IsLockedOutAsync(userId.Value, cancellationToken))
            return Fail(destination, userId, AccountErrors.LockedOut);

        var account = await accounts.ReadAsync(userId.Value, cancellationToken);
        if (account is null || account.Status is UserStatus.Suspended or UserStatus.Deleted)
            return Fail(destination, userId, AccountErrors.Suspended);
        if (account.IsPlatformOperator && selection?.Access != Access.Platform)
            return Fail(destination, userId, AccessErrors.PlatformOnly);
        var method = (await loginMethods.ListByUserIdAsync(userId.Value, cancellationToken))
            .SingleOrDefault(method => method.Type == LoginDestinationResolver.Type(destination) && method.Value == destination.Value);
        if (method is null || !await availability.IsAvailableAsync(userId.Value, method, cancellationToken))
            return Fail(destination, userId, LoginCodeErrors.Invalid(attemptsLeft: null));

        if (account.Status == UserStatus.PendingDeletion)
            return Fail(destination, userId, cancellation.IssueForLoginCode(account, request.ReturnUrl!));

        if (selection?.Access == Access.Business)
        {
            var selected = await connect.GetActiveUserAsync(userId.Value, selection.Access,
                selection.TenantId, cancellationToken);
            if (selected.IsFailure) return Fail(destination, userId, selected.Error);
        }

        if (account.IsPlatformOperator && selection?.Access == Access.Platform)
        {
            // E9 (ADR 0043): recién ahora la cuenta probó el primer factor. La sesión, la invitación, el borrado de los
            // fallos y el ingreso exitoso esperan al segundo factor.
            var pending = accounts.CheckOperatorInvitation(account);
            if (pending.IsFailure) return Fail(destination, userId, pending.Error);
            return new LoginVerification(userId.Value, NeedsPlatformFactor: true);
        }

        var operatorInvitation = await accounts.AcceptOperatorInvitationAsync(account, request.ReturnUrl!, cancellationToken);
        if (operatorInvitation.IsFailure) return Fail(destination, userId, operatorInvitation.Error);

        await attempts.ResetFailedAttemptsAsync(userId.Value, cancellationToken);
        attempts.RecordSuccess(destination, userId.Value);
        return new LoginVerification(userId.Value, NeedsPlatformFactor: false);
    }

    private Error Fail(LoginCodeDestination destination, Guid? userId, Error error)
    {
        return attempts.RecordFailure(destination, userId, error);
    }
}

/// <summary>
/// La cuenta que probó el código y si es un operador que entra a Plataforma: entonces el ingreso espera el segundo
/// factor (E9), sin sesión todavía.
/// </summary>
internal sealed record LoginVerification(Guid UserId, bool NeedsPlatformFactor);
