using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>
/// Coordina el código de correo, Identity y el outbox en una transacción por operación.
/// La cookie se escribe solamente después de confirmar el código consumido y la auditoría.
/// </summary>
internal sealed class LoginCodeService(
    LoginCodeIssuer issuer,
    LoginCodeVerifier verifier,
    ILoginCodeRepository codes,
    IUserLookup userLookup,
    IUserRepository users,
    ILoginAuditRepository audits,
    IEnumerable<ILoginCodeChannel> channels,
    UserCultures userCultures,
    ISignInService signIn,
    IRequestValidator validator,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<LoginCodeService> logger) : ILoginCodeService
{
    public Task<Result<RequestLoginCodeResponse>> RequestLoginCodeAsync(
        RequestLoginCodeRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<RequestLoginCodeResponse>(logger, timeProvider, "RequestLoginCode", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await unitOfWork.ExecuteInTransactionAsync(
                ct => RequestCoreAsync(request, ct), CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result<VerifyLoginCodeResponse>> VerifyLoginCodeAsync(
        VerifyLoginCodeRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<VerifyLoginCodeResponse>(logger, timeProvider, "VerifyLoginCode", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var verified = await unitOfWork.ExecuteInTransactionAsync(
                ct => VerifyCoreAsync(request, ct), CommitPolicy.OnAnyResult, cancellationToken);
            if (verified.IsFailure) return verified.Error;

            await signIn.SignInAsync(verified.Value, cancellationToken);
            return new VerifyLoginCodeResponse(request.ReturnUrl!);
        });

    private async Task<Result<RequestLoginCodeResponse>> RequestCoreAsync(
        RequestLoginCodeRequest request, CancellationToken cancellationToken)
    {
        var destination = LoginCodeDestination.ForEmail(request.Email!);
        var issued = await issuer.IssueLoginCodeAsync(destination, cancellationToken);
        if (issued.IsFailure) return issued.Error;

        var userId = await userLookup.FindVerifiedUserIdAsync(
            LoginMethodType.Email, destination.Value, cancellationToken);
        if (userId is { } id)
        {
            var account = await users.GetByIdAsync(id, cancellationToken);
            if (account is { Status: UserStatus.Active } && !await signIn.IsLockedOutAsync(id, cancellationToken))
            {
                var culture = await userCultures.ResolveAsync(account.Culture, null, cancellationToken);
                var channel = channels.SingleOrDefault(value => value.Key == destination.Channel)
                    ?? throw new InvalidOperationException("The email login channel is not registered.");
                channel.Enqueue(destination.Value, issued.Value.Code, issued.Value.LifetimeMinutes, culture);
                issued.Value.LoginCode.MarkSent(issued.Value.IssuedAtUtc);
            }
        }

        return new RequestLoginCodeResponse(issued.Value.ResendCooldownSeconds);
    }

    private async Task<Result<Guid>> VerifyCoreAsync(
        VerifyLoginCodeRequest request, CancellationToken cancellationToken)
    {
        var destination = LoginCodeDestination.ForEmail(request.Email!);
        await codes.LockDestinationAsync(destination, cancellationToken);
        var userId = await userLookup.FindVerifiedUserIdAsync(
            LoginMethodType.Email, destination.Value, cancellationToken);
        if (userId is { } id && await signIn.IsLockedOutAsync(id, cancellationToken))
        {
            return Fail(userId, AccountErrors.LockedOut);
        }

        var verified = await verifier.VerifyAsync(destination, LoginCodePurpose.Login,
            requestedByUserId: null, request.Code!, cancellationToken);
        if (verified.IsFailure)
        {
            if (userId is { } existingUserId)
            {
                await signIn.RegisterFailedAttemptAsync(existingUserId, cancellationToken);
            }
            return Fail(userId, verified.Error);
        }

        if (userId is null)
        {
            return Fail(null, LoginCodeErrors.Invalid(attemptsLeft: null));
        }

        var account = await users.GetByIdAsync(userId.Value, cancellationToken);
        if (account is null || account.Status is UserStatus.Suspended or UserStatus.Deleted)
        {
            return Fail(userId, AccountErrors.Suspended);
        }
        if (account.Status == UserStatus.PendingDeletion)
        {
            return Fail(userId, AccountErrors.PendingDeletion);
        }

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
