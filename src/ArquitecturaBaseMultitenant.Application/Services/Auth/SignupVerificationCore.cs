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
    SignupExistingMethodVerifier existingMethods, ILoginAuditRepository audits, TimeProvider timeProvider)
{
    internal async Task<Result<Guid>> VerifyAsync(VerifySignupRequest request, PersonalSpaceDraft draft,
        CancellationToken cancellationToken)
    {
        var destination = LoginCodeDestination.ForEmail(request.Email!);
        await codes.LockDestinationAsync(destination, cancellationToken);
        var method = await userLookup.FindMethodAsync(LoginMethodType.Email,
            destination.Value, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        (LegalDocument Terms, LegalDocument Privacy)? documents = null;
        if (method is null)
        {
            var allowed = await policy.CanRegisterAsync(cancellationToken);
            if (allowed.IsFailure) return Fail(null, allowed.Error);
            documents = await registrar.GetCurrentLegalAsync(nowUtc, cancellationToken);
        }
        else if (await existingMethods.CheckAccountAsync(method.UserId, cancellationToken) is { } accountError)
        {
            return Fail(method.UserId, accountError);
        }

        var verified = await verifier.VerifyAsync(destination, LoginCodePurpose.Signup,
            requestedByUserId: null, request.Code!, cancellationToken);
        if (verified.IsFailure) return Fail(method?.UserId, verified.Error);

        var userId = method is null
            ? await registrar.RegisterAsync(request.Email!, draft, nowUtc,
                documents!.Value.Terms, documents.Value.Privacy, cancellationToken)
            : await existingMethods.ConfirmAsync(method, request.Email!, nowUtc, cancellationToken);
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
