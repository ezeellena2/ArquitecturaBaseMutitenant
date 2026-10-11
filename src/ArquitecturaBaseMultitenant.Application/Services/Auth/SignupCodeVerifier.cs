using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Application.Models.Common;
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
    ISignInService signIn, TimeProvider timeProvider, LoginDestinationResolver destinations)
{
    internal Result<LoginCodeDestination> Resolve(Email? email,
        PhoneInputRequest? phone, string? channel) => destinations.Resolve(email, phone, channel);

    internal async Task<Result<Guid>> VerifyAsync(VerifySignupRequest request, LoginCodeDestination destination, PersonalSpaceDraft draft,
        CancellationToken cancellationToken)
    {
        var attemptedMethod = await existingMethods.LockAsync(destination, cancellationToken);
        if (LoginDestinationResolver.Type(destination) == LoginMethodType.Phone && attemptedMethod is { VerifiedAtUtc: not null })
            return Fail(destination, attemptedMethod.UserId, FieldErrors.On(SignupErrors.PhoneTaken, "phone"));
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var verified = await verifier.VerifyAsync(destination, LoginCodePurpose.Signup,
            requestedByUserId: null, request.Code!, attemptedMethod?.UserId, cancellationToken);
        if (verified.IsFailure) return Fail(destination, attemptedMethod?.UserId, verified.Error);

        var method = await existingMethods.FindAsync(destination, cancellationToken);
        Guid userId;
        if (method is null || method.VerifiedAtUtc is null)
        {
            var validNew = destinations.ValidateNew(destination);
            if (validNew.IsFailure) return Fail(destination, null, validNew.Error);
            var allowed = await policy.CanRegisterAsync(cancellationToken);
            if (allowed.IsFailure) return Fail(destination, null, allowed.Error);
            (LegalDocument Terms, LegalDocument Privacy) documents =
                await registrar.GetCurrentLegalAsync(nowUtc, cancellationToken);
            if (method is not null)
                await existingMethods.RemovePendingAsync(method, cancellationToken);
            userId = await registrar.RegisterAsync(destination, draft, nowUtc,
                documents.Terms, documents.Privacy, cancellationToken);
        }
        else
        {
            if (await signIn.IsLockedOutAsync(method.UserId, cancellationToken))
                return Fail(destination, method.UserId, AccountErrors.LockedOut);
            if (await existingMethods.CheckAccountAsync(method.UserId, cancellationToken) is { } accountError)
                return Fail(destination, method.UserId, accountError);
            userId = await existingMethods.ConfirmAsync(method, nowUtc, cancellationToken);
            await signIn.ResetFailedAttemptsAsync(userId, cancellationToken);
        }

        audits.Add(LoginAudit.Success(userId, destinations.AuditMethod(destination), nowUtc));
        return userId;
    }

    internal async Task<Result<Guid>> VerifyBusinessAsync(VerifySignupRequest request, LoginCodeDestination destination, PersonalSpaceDraft defaults,
        CancellationToken cancellationToken)
    {
        var attemptedMethod = await existingMethods.LockAsync(destination, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var allowed = await policy.CanRegisterBusinessAsync(cancellationToken);
        if (allowed.IsFailure) return Fail(destination, null, allowed.Error);

        var ownerId = LoginDestinationResolver.Type(destination) == LoginMethodType.Phone && attemptedMethod is { VerifiedAtUtc: not null }
            ? (Guid?)attemptedMethod.UserId : null;
        var verified = await verifier.VerifyAsync(destination, LoginCodePurpose.BusinessSignup,
            ownerId, request.Code!, attemptedMethod?.UserId, cancellationToken);
        if (verified.IsFailure) return Fail(destination, attemptedMethod?.UserId, verified.Error);

        var method = await existingMethods.FindAsync(destination, cancellationToken);
        Guid userId;
        if (method is null || method.VerifiedAtUtc is null)
        {
            var validNew = destinations.ValidateNew(destination);
            if (validNew.IsFailure) return Fail(destination, null, validNew.Error);
            var documents = await registrar.GetCurrentLegalAsync(nowUtc, cancellationToken);
            if (method is not null) await existingMethods.RemovePendingAsync(method, cancellationToken);
            userId = await registrar.RegisterBusinessAsync(destination, defaults, nowUtc,
                documents.Terms, documents.Privacy, cancellationToken);
        }
        else
        {
            if (await signIn.IsLockedOutAsync(method.UserId, cancellationToken))
                return Fail(destination, method.UserId, AccountErrors.LockedOut);
            if (await existingMethods.CheckAccountAsync(method.UserId, cancellationToken) is { } accountError)
                return Fail(destination, method.UserId, accountError);
            userId = await existingMethods.ConfirmAsync(method, nowUtc, cancellationToken);
            await signIn.ResetFailedAttemptsAsync(userId, cancellationToken);
            var documents = await registrar.GetCurrentLegalAsync(nowUtc, cancellationToken);
            await registrar.AcceptCurrentLegalAsync(userId, documents.Terms, documents.Privacy,
                nowUtc, cancellationToken);
        }

        audits.Add(LoginAudit.Success(userId, destinations.AuditMethod(destination), nowUtc));
        return userId;
    }

    private Error Fail(LoginCodeDestination destination, Guid? userId, Error error)
    {
        audits.Add(LoginAudit.Failure(userId, destinations.AuditMethod(destination), error,
            timeProvider.GetUtcNow().UtcDateTime));
        return error;
    }
}
