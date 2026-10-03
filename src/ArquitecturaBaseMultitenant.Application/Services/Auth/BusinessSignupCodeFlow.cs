using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Orquesta el código y la verificación del acceso empresarial antes de crear la organización.</summary>
internal sealed class BusinessSignupCodeFlow(SignupCodeRequester codes, SignupVerificationCore verification,
    IPersonalSpaceProvisioner defaults, IUnitOfWork unitOfWork, ISignInService signIn)
{
    internal Task<Result<RequestLoginCodeResponse>> RequestEmailCodeAsync(SignupRequest request,
        CancellationToken cancellationToken) => unitOfWork.ExecuteInTransactionAsync(
        ct => codes.RequestBusinessSignupAsync(request, ct), CommitPolicy.OnSuccess, cancellationToken);

    internal async Task<Result> VerifyEmailCodeAsync(VerifySignupRequest request,
        CancellationToken cancellationToken)
    {
        var draft = await defaults.PrepareAsync(request.Culture, request.TimeZoneId, cancellationToken);
        var verified = await unitOfWork.ExecuteInTransactionAsync(
            ct => verification.VerifyBusinessAsync(request, draft, ct), CommitPolicy.OnAnyResult, cancellationToken);
        if (verified.IsFailure) return verified.Error;
        await signIn.SignInAsync(verified.Value, cancellationToken);
        return Result.Success();
    }
}
