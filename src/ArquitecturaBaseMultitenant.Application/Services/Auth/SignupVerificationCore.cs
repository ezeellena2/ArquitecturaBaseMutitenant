using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Bloquea, relee y verifica el código antes de registrar o confirmar una cuenta.</summary>
internal sealed class SignupVerificationCore(ILoginCodeRepository codes, IUserLookup userLookup,
    LoginCodeVerifier verifier, SignupPolicy policy, SignupAccountRegistrar registrar,
    SignupExistingMethodVerifier existingMethods, ILoginAuditRepository audits,
    ISignInService signIn, TimeProvider timeProvider)
{
    internal async Task<Result<Guid>> VerifyAsync(VerifySignupRequest request, PersonalSpaceDraft draft,
        CancellationToken cancellationToken)
    {
        var destination = LoginCodeDestination.ForEmail(request.Email!);
        await codes.LockDestinationAsync(destination, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var currentCode = await codes.GetLatestAsync(destination, LoginCodePurpose.Signup,
            requestedByUserId: null, cancellationToken);
        var hadActiveCode = currentCode?.IsActive(nowUtc) == true;
        var verified = await verifier.VerifyAsync(destination, LoginCodePurpose.Signup,
            requestedByUserId: null, request.Code!, cancellationToken);
        if (verified.IsFailure)
        {
            var failedMethod = await userLookup.FindMethodAsync(LoginMethodType.Email,
                destination.Value, cancellationToken);
            if (failedMethod is not null && hadActiveCode && IsFailedCodeAttempt(verified.Error))
            {
                await signIn.RegisterFailedAttemptAsync(failedMethod.UserId, cancellationToken);
                if (await signIn.IsLockedOutAsync(failedMethod.UserId, cancellationToken))
                    return Fail(failedMethod.UserId, AccountErrors.LockedOut);
            }

            return Fail(failedMethod?.UserId, verified.Error);
        }

        var method = await userLookup.FindMethodAsync(LoginMethodType.Email,
            destination.Value, cancellationToken);
        Guid userId;
        if (method is null)
        {
            var allowed = await policy.CanRegisterAsync(cancellationToken);
            if (allowed.IsFailure) return Fail(null, allowed.Error);
            (LegalDocument Terms, LegalDocument Privacy) documents =
                await registrar.GetCurrentLegalAsync(nowUtc, cancellationToken);
            userId = await registrar.RegisterAsync(request.Email!, draft, nowUtc,
                documents.Terms, documents.Privacy, cancellationToken);
        }
        else
        {
            if (await signIn.IsLockedOutAsync(method.UserId, cancellationToken))
                return Fail(method.UserId, AccountErrors.LockedOut);
            if (await existingMethods.CheckAccountAsync(method.UserId, cancellationToken) is { } accountError)
                return Fail(method.UserId, accountError);
            userId = await existingMethods.ConfirmAsync(method, request.Email!, nowUtc, cancellationToken);
            await signIn.ResetFailedAttemptsAsync(userId, cancellationToken);
        }

        audits.Add(LoginAudit.Success(userId, LoginAuditMethod.Code, nowUtc));
        return userId;
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
