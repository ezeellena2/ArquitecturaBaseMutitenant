using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Text;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Preview sin efectos; canje y auditoría atómicos, cookie personal después del commit.</summary>
internal sealed class LoginLinkService(LoginLinkGuard guard, LoginLinkVerifier verifier, ISecureTokenGenerator tokens,
    ISignInService signIn, IRequestValidator validator, IUnitOfWork unitOfWork, TimeProvider clock, ILogger<LoginLinkService> logger) : ILoginLinkService
{
    public Task<Result<LoginLinkPreviewResponse>> PreviewAsync(PreviewLoginLinkRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<LoginLinkPreviewResponse>(logger, clock, "PreviewLoginLink", async () =>
        {
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            var link = await guard.FindAsync(tokens.Hash(request.Token!), ct);
            if (link is null || link.ConsumedAtUtc is not null || link.InvalidatedAtUtc is not null
                || clock.GetUtcNow().UtcDateTime >= link.ExpiresAtUtc) return LoginLinkErrors.Invalid;
            var account = await guard.AccountAsync(link, ct);
            if (account is null) return LoginLinkErrors.Invalid;
            if (account.Status == UserStatus.Suspended) return AccountErrors.Suspended;
            // Fresh coherent projection after awaited availability checks: no stale preview of a converted account.
            var fresh = await guard.FindAsync(tokens.Hash(request.Token!), ct);
            if (fresh is null || fresh.Account.IsPlatformOperator || fresh.Account.Status == UserStatus.Deleted
                || fresh.ConsumedAtUtc is not null || fresh.InvalidatedAtUtc is not null
                || clock.GetUtcNow().UtcDateTime >= fresh.ExpiresAtUtc || !guard.IsCurrentPhoneAvailable(fresh))
                return LoginLinkErrors.Invalid;
            if (fresh.Account.Status == UserStatus.Suspended) return AccountErrors.Suspended;
            if (fresh.LockoutEnabled && fresh.LockoutEndUtc > clock.GetUtcNow().UtcDateTime) return AccountErrors.LockedOut;
            return new LoginLinkPreviewResponse(fresh.Account.DisplayName, PersonalDataMask.Phone(fresh.Method.Value));
        });

    public Task<Result<LoginLinkRedeemResponse>> RedeemAsync(RedeemLoginLinkRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<LoginLinkRedeemResponse>(logger, clock, "RedeemLoginLink", async () =>
        {
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            var result = await unitOfWork.ExecuteInTransactionAsync(
                c => verifier.RedeemAsync(tokens.Hash(request.Token!), c), CommitPolicy.OnAnyResult, ct);
            if (result.IsFailure) return result.Error;
            await signIn.SignInAsync(result.Value, Access.Consumer, ct);
            return new LoginLinkRedeemResponse("consumer");
        });
}
