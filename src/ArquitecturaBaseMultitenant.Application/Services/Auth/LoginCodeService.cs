using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Una UoW por pedido o verificación; la cookie sale después del commit.</summary>
internal sealed class LoginCodeService(
    LoginCodeRequester requester,
    LoginCodeVerificationFlow verification,
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
                ct => requester.RequestAsync(request, ct), CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result<VerifyLoginCodeResponse>> VerifyLoginCodeAsync(
        VerifyLoginCodeRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<VerifyLoginCodeResponse>(logger, timeProvider, "VerifyLoginCode", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var verified = await unitOfWork.ExecuteInTransactionAsync(
                ct => verification.VerifyAsync(request, ct), CommitPolicy.OnAnyResult, cancellationToken);
            if (verified.IsFailure) return verified.Error;

            await signIn.SignInAsync(verified.Value, cancellationToken);
            return new VerifyLoginCodeResponse(request.ReturnUrl!);
        });
}
