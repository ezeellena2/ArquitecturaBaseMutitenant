using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
namespace ArquitecturaBaseMultitenant.Application.Services.Auth;
internal sealed class LoginLinkGuard(ILoginLinkRepository links, ILoginMethodRepository methods,
    LoginMethodAvailabilityPolicy availability)
{
    internal Task<LoginLinkSnapshot?> FindAsync(string hash, CancellationToken ct) => links.FindAsync(hash, ct);
    internal bool IsCurrentPhoneAvailable(LoginLinkSnapshot link) =>
        !link.Account.IsPlatformOperator && link.Account.Status != Domain.Users.UserStatus.Deleted
        && link.Method is { Type: LoginMethodType.Phone, VerifiedAtUtc: not null, ManagedByTenantId: null }
        && availability.SignInTypes().Contains(LoginMethodType.Phone);
    internal async Task<UserAccountRow?> AccountAsync(LoginLinkSnapshot link, CancellationToken ct)
    {
        var account = link.Account;
        if (account is null || account.IsPlatformOperator || account.Status == Domain.Users.UserStatus.Deleted) return null;
        var method = link.Method;
        return method is { Type: LoginMethodType.Phone, VerifiedAtUtc: not null }
            && await availability.IsAvailableAsync(link.UserId, method, ct) ? account : null;
    }
    internal async Task<(LoginLink Link, LoginLinkSnapshot Snapshot)?> LockAsync(string hash, CancellationToken ct)
    {
        var hint = await links.FindAsync(hash, ct);
        if (hint is null) return null;
        await methods.LockUserAsync(hint.UserId, ct);
        var link = await links.GetAsync(hash, ct);
        if (link is null || link.UserId != hint.UserId || link.MethodId != hint.MethodId) return null;
        var fresh = await links.FindAsync(hash, ct);
        var account = fresh is null ? null : await AccountAsync(fresh, ct);
        return account is null ? null : (link, fresh!);
    }
}
