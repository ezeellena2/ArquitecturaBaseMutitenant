using System.Security.Claims;
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
    ApplicationDbContext context) : ISignInService
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

    public async Task SignInAsync(Guid userId, CancellationToken cancellationToken)
    {
        context.RequireNoTransaction();
        await signInManager.SignInAsync(await RequireUserAsync(userId, cancellationToken), isPersistent: true);
    }

    public async Task<ExternalLogin?> GetExternalLoginAsync(CancellationToken cancellationToken)
    {
        var info = await signInManager.GetExternalLoginInfoAsync();
        return info is null ? null : new ExternalLogin(
            info.LoginProvider,
            info.ProviderKey,
            ParseEmail(info.Principal.FindFirstValue(ClaimTypes.Email)),
            string.Equals(info.Principal.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase),
            info.Principal.FindFirstValue(ClaimTypes.Name));
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
