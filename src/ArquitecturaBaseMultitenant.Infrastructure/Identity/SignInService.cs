using System.Security.Claims;
using System.Security.Cryptography;
using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity;

/// <summary>Identity técnico: los cambios de bloqueo y revocación exigen el límite del caso de uso.</summary>
internal sealed class SignInService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ITokenRevoker tokenRevoker,
    ApplicationDbContext context, TimeProvider timeProvider) : ISignInService
{
    public async Task<bool> IsLockedOutAsync(Guid userId, CancellationToken cancellationToken) =>
        await userManager.IsLockedOutAsync(await RequireUserAsync(userId, cancellationToken));

    public async Task RegisterFailedAttemptAsync(Guid userId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        EnsureSucceeded(await userManager.AccessFailedAsync(await RequireUserAsync(userId, cancellationToken)),
            "register the failed attempt");
    }

    public async Task ResetFailedAttemptsAsync(Guid userId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        EnsureSucceeded(await userManager.ResetAccessFailedCountAsync(await RequireUserAsync(userId, cancellationToken)),
            "reset the failed attempts");
    }

    public async Task RevokeSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var user = await RequireUserAsync(userId, cancellationToken);
        EnsureSucceeded(await userManager.UpdateSecurityStampAsync(user), "renew the security stamp");
        await tokenRevoker.RevokeUserAsync(userId, cancellationToken);
    }

    public async Task SignInAsync(Guid userId, Access access, CancellationToken cancellationToken)
    {
        context.RequireNoTransaction();
        var properties = new AuthenticationProperties { IsPersistent = true };
        properties.Items[BrowserSessionKeys.CookieSessionId] =
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        properties.Items[BrowserSessionKeys.StartedAtUtc] =
            BrowserSessionKeys.FormatStartedAtUtc(timeProvider.GetUtcNow().UtcDateTime);
        signInManager.AuthenticationScheme = access == Access.Platform
            ? BrowserSessionKeys.PlatformScheme : IdentityConstants.ApplicationScheme;
        await signInManager.SignInAsync(await RequireUserAsync(userId, cancellationToken), properties);
    }

    public async Task SignInPlatformAsync(Guid userId, string sessionId, DateTime startedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        context.RequireNoTransaction();
        var properties = new AuthenticationProperties { IsPersistent = true };
        properties.Items[BrowserSessionKeys.CookieSessionId] = sessionId;
        properties.Items[BrowserSessionKeys.StartedAtUtc] = BrowserSessionKeys.FormatStartedAtUtc(startedAtUtc);
        signInManager.AuthenticationScheme = BrowserSessionKeys.PlatformScheme;
        await signInManager.SignInAsync(await RequireUserAsync(userId, cancellationToken), properties);
    }

    // Sin tracking: la guarda de prueba reciente lo lee antes de la transacción, y una cuenta seguida desde ahí haría que
    // el caso de uso la releyera vieja después de sus locks.
    public async Task<string> GetSecurityStampAsync(Guid userId, CancellationToken cancellationToken) =>
        await context.Users.AsNoTracking()
            .Where(user => user.Id == userId && user.Status != UserStatus.Deleted)
            .Select(user => user.SecurityStamp)
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new InvalidOperationException("The user has no security stamp.");

    public async Task<DateTime?> GetLockoutEndUtcAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(userId, cancellationToken);
        if (!await userManager.IsLockedOutAsync(user)) return null;
        return (await userManager.GetLockoutEndDateAsync(user))?.UtcDateTime;
    }

    public async Task<ExternalLogin?> GetExternalLoginAsync(CancellationToken cancellationToken)
    {
        var info = await signInManager.GetExternalLoginInfoAsync();
        return info is null ? null : new ExternalLogin(
            info.LoginProvider,
            info.ProviderKey,
            ParseEmail(info.Principal.FindFirstValue(ClaimTypes.Email)),
            string.Equals(info.Principal.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase),
            info.Principal.FindFirstValue(ClaimTypes.Name),
            info.Principal.FindFirstValue(ClaimTypes.GivenName),
            info.Principal.FindFirstValue(ClaimTypes.Surname));
    }

    private static Email? ParseEmail(string? value)
    {
        var result = Email.Create(value);
        return result.IsSuccess ? result.Value : null;
    }

    public Task SignOutExternalAsync(CancellationToken cancellationToken) =>
        signInManager.Context.SignOutAsync(IdentityConstants.ExternalScheme);

    private async Task<ApplicationUser> RequireUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await userManager.Users.FirstOrDefaultAsync(
            user => user.Id == userId && user.Status != UserStatus.Deleted, cancellationToken)
        ?? throw new InvalidOperationException("The user does not exist.");

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not {action}: {string.Join(", ", result.Errors.Select(error => error.Code))}.");
        }
    }
}
