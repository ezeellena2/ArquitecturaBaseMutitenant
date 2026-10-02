using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Verifica identidad empresarial por correo antes de abrir el formulario de organización.</summary>
internal sealed class BusinessSignupService(SignupCodeRequester codes, SignupVerificationCore verification,
    IPersonalSpaceProvisioner defaults, BusinessOrganizationCreator organizationCreator,
    IRequestValidator validator, IUnitOfWork unitOfWork,
    ISignInService signIn, TimeProvider timeProvider, ILogger<BusinessSignupService> logger)
    : IBusinessSignupService
{
    public Task<Result<RequestLoginCodeResponse>> RequestEmailCodeAsync(SignupRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<RequestLoginCodeResponse>(logger, timeProvider, "RequestBusinessSignupCode", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await unitOfWork.ExecuteInTransactionAsync(
                ct => codes.RequestBusinessSignupAsync(request, ct), CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result> VerifyEmailCodeAsync(VerifySignupRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "VerifyBusinessSignupCode", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var draft = await defaults.PrepareAsync(request.Culture, request.TimeZoneId, cancellationToken);
            var verified = await unitOfWork.ExecuteInTransactionAsync(
                ct => verification.VerifyBusinessAsync(request, draft, ct), CommitPolicy.OnAnyResult,
                cancellationToken);
            if (verified.IsFailure) return verified.Error;

            await signIn.SignInAsync(verified.Value, cancellationToken);
            return Result.Success();
        });

    public Task<Result<BusinessSignupResponse>> CreateOrganizationAsync(Guid userId, Guid operationId,
        BusinessSignupRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<BusinessSignupResponse>(logger, timeProvider, "CreateBusinessSignupOrganization",
            async () =>
            {
                ArgumentNullException.ThrowIfNull(request);
                if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
                return await organizationCreator.CreateAsync(userId, operationId, request, cancellationToken);
            });
}
