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

/// <summary>Una UoW por pedido o verificación; la cookie sale después del commit.</summary>
internal sealed class LoginCodeService(
    LoginCodeRequester requester,
    LoginVerifier verification,
    ISignInService signIn,
    LoginInvitationResolver invitations,
    ITenantScope tenantScope,
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
            var invitation = await invitations.PrepareAsync(request.Email!, request.ReturnUrl!, cancellationToken);
            using var scope = invitation is null ? null : tenantScope.Enter(invitation.DefaultsTenantId);
            var verified = await unitOfWork.ExecuteInTransactionAsync(
                ct => verification.VerifyAsync(request, invitation, ct), CommitPolicy.OnAnyResult, cancellationToken);
            if (verified.IsFailure) return verified.Error;

            var access = ReturnUrls.TryReadAccessSelection(request.ReturnUrl, out var selection)
                ? selection.Access : Access.Business;
            await signIn.SignInAsync(verified.Value, access, cancellationToken);
            return new VerifyLoginCodeResponse(request.ReturnUrl!);
        });
}
