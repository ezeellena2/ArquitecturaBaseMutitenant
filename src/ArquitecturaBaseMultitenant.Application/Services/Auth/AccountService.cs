using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Registro por correo: el código prueba posesión y el alta completa es atómica.</summary>
internal sealed class AccountService(SignupCodeRequester requester, IPersonalSpaceProvisioner personalSpaces,
    SignupCodeVerifier verification,
    IRequestValidator validator, IUnitOfWork unitOfWork, ITenantScope tenantScope,
    ISignInService signIn, TimeProvider timeProvider,
    ILogger<AccountService> logger) : IAccountService
{
    public Task<Result<RequestLoginCodeResponse>> RequestSignupCodeAsync(SignupRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<RequestLoginCodeResponse>(logger, timeProvider, "RequestSignupCode", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await unitOfWork.ExecuteInTransactionAsync(
                ct => requester.RequestAsync(request, ct), CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result> VerifySignupAsync(VerifySignupRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "VerifySignup", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var draft = await personalSpaces.PrepareAsync(request.Culture, request.TimeZoneId, cancellationToken);
            using var scope = tenantScope.Enter(draft.Tenant.Id);
            var verified = await unitOfWork.ExecuteInTransactionAsync(
                ct => verification.VerifyAsync(request, draft, ct), CommitPolicy.OnAnyResult, cancellationToken);
            if (verified.IsFailure) return verified.Error;

            await signIn.SignInAsync(verified.Value, Access.Consumer, cancellationToken);
            return Result.Success();
        });
}
