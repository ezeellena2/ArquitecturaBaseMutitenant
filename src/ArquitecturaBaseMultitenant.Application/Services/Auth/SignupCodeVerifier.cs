using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Bloquea, relee y verifica el código antes de registrar o confirmar una cuenta.</summary>
internal sealed class SignupCodeVerifier(LoginCodeAttemptVerifier verifier, SignupPolicy policy, SignupAccountRegistrar registrar,
    SignupExistingMethodVerifier existingMethods, ILoginAuditRepository audits,
    ISignInService signIn, TimeProvider timeProvider)
{
    internal async Task<Result<Guid>> VerifyAsync(VerifySignupRequest request, PersonalSpaceDraft draft,
        CancellationToken cancellationToken)
    {
        var destination = LoginCodeDestination.ForEmail(request.Email!);
        var attemptedMethod = await existingMethods.LockAsync(destination, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var verified = await verifier.VerifyAsync(destination, LoginCodePurpose.Signup,
            requestedByUserId: null, request.Code!, attemptedMethod?.UserId, cancellationToken);
        if (verified.IsFailure) return Fail(attemptedMethod?.UserId, verified.Error);

        var method = await existingMethods.FindAsync(destination, cancellationToken);
        Guid userId;
        if (method is null || method.VerifiedAtUtc is null)
        {
            var allowed = await policy.CanRegisterAsync(cancellationToken);
            if (allowed.IsFailure) return Fail(null, allowed.Error);
            (LegalDocument Terms, LegalDocument Privacy) documents =
                await registrar.GetCurrentLegalAsync(nowUtc, cancellationToken);
            if (method is not null)
                await existingMethods.RemovePendingAsync(method, cancellationToken);
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

    internal async Task<Result<Guid>> VerifyBusinessAsync(VerifySignupRequest request, PersonalSpaceDraft defaults,
        CancellationToken cancellationToken)
    {
        var destination = LoginCodeDestination.ForEmail(request.Email!);
        var attemptedMethod = await existingMethods.LockAsync(destination, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var allowed = await policy.CanRegisterBusinessAsync(cancellationToken);
        if (allowed.IsFailure) return Fail(null, allowed.Error);

        var verified = await verifier.VerifyAsync(destination, LoginCodePurpose.BusinessSignup,
            requestedByUserId: null, request.Code!, attemptedMethod?.UserId, cancellationToken);
        if (verified.IsFailure) return Fail(attemptedMethod?.UserId, verified.Error);

        var method = await existingMethods.FindAsync(destination, cancellationToken);
        Guid userId;
        if (method is null || method.VerifiedAtUtc is null)
        {
            var documents = await registrar.GetCurrentLegalAsync(nowUtc, cancellationToken);
            if (method is not null) await existingMethods.RemovePendingAsync(method, cancellationToken);
            userId = await registrar.RegisterBusinessAsync(request.Email!, defaults, nowUtc,
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
            var documents = await registrar.GetCurrentLegalAsync(nowUtc, cancellationToken);
            await registrar.AcceptCurrentLegalAsync(userId, documents.Terms, documents.Privacy,
                nowUtc, cancellationToken);
        }

        audits.Add(LoginAudit.Success(userId, LoginAuditMethod.Code, nowUtc));
        return userId;
    }

    private Error Fail(Guid? userId, Error error)
    {
        audits.Add(LoginAudit.Failure(userId, LoginAuditMethod.Code, error,
            timeProvider.GetUtcNow().UtcDateTime));
        return error;
    }
}
