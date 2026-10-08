using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;
namespace ArquitecturaBaseMultitenant.Application.Services.Auth;
internal sealed class LoginLinkReplayService(LoginLinkGuard guard, ISecureTokenGenerator tokens,
    ISignInService signIn, TimeProvider clock, ILogger<LoginLinkReplayService> logger) : ILoginLinkReplayService
{
    public Task<Result> ValidateAsync(RedeemLoginLinkRequest request, LoginLinkReplayOutcome outcome, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "ReplayLoginLink", async () =>
        {
            if (outcome == LoginLinkReplayOutcome.Rejected || request.Access != "consumer" || !ISecureTokenGenerator.HasTokenFormat(request.Token)) return Result.Failure(LoginLinkErrors.Invalid);
            var link = await guard.FindAsync(tokens.Hash(request.Token!), ct);
            if (link is null || link.ConsumedAtUtc is null || link.InvalidatedAtUtc is not null) return Result.Failure(LoginLinkErrors.Invalid);
            var account = await guard.AccountAsync(link, ct);
            var expectedStatus = outcome == LoginLinkReplayOutcome.PendingDeletion ? UserStatus.PendingDeletion : UserStatus.Active;
            if (account is null || account.Status != expectedStatus || await signIn.IsLockedOutAsync(account.Id, ct))
                return Result.Failure(LoginLinkErrors.Invalid);
            var fresh = await guard.FindAsync(tokens.Hash(request.Token!), ct);
            if (fresh is null || fresh.ConsumedAtUtc is null || fresh.InvalidatedAtUtc is not null
                || fresh.Account.Status != expectedStatus || fresh.Account.IsPlatformOperator
                || fresh.LockoutEnabled && fresh.LockoutEndUtc > clock.GetUtcNow().UtcDateTime
                || !guard.IsCurrentPhoneAvailable(fresh)) return Result.Failure(LoginLinkErrors.Invalid);
            return Result.Success();
        });
}
