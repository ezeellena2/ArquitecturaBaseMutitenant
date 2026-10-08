using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Emite y revoca pruebas del método dentro de la UoW del llamador, sin abrir otra.</summary>
internal sealed class LoginLinkIssuer(IUserRepository users, ILoginMethodRepository methods, ILoginLinkRepository links,
    LoginMethodAvailabilityPolicy availability, ISecureTokenGenerator tokens, IOptions<LoginLinkOptions> options, TimeProvider clock) : ILoginLinkIssuer
{
    public async Task<Result<IssuedLoginLink>> IssueAsync(Guid userId, Guid methodId, CancellationToken ct)
    {
        await methods.LockUserAsync(userId, ct);
        var account = await users.GetByIdAsync(userId, ct);
        if (account is null || account.IsPlatformOperator || account.Status is UserStatus.Suspended or UserStatus.Deleted)
            return LoginLinkErrors.Invalid;
        var owner = await links.FindOwnerAsync(userId, methodId, ct);
        if (owner is null || owner.Account.IsPlatformOperator || owner.Account.Status is UserStatus.Suspended or UserStatus.Deleted
            || owner.Method is not { Type: LoginMethodType.Phone, VerifiedAtUtc: not null }
            || !await availability.IsAvailableAsync(userId, owner.Method, ct)) return LoginLinkErrors.Invalid;
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        if (owner.LockoutEnabled && owner.LockoutEndUtc > nowUtc) return LoginLinkErrors.Invalid;
        var window = TimeSpan.FromMinutes(options.Value.RequestWindowMinutes);
        var cooldown = TimeSpan.FromSeconds(options.Value.ResendCooldownSeconds);
        var times = await links.ListIssueTimesSinceAsync(userId, nowUtc - (window > cooldown ? window : cooldown), ct);
        if (times.Count != 0)
        {
            var allowedAtUtc = times[^1] + cooldown;
            var inWindow = times.Where(value => value > nowUtc - window).ToArray();
            var excess = inWindow.Length - options.Value.MaxRequestsPerWindow;
            if (excess >= 0 && inWindow[excess] + window > allowedAtUtc) allowedAtUtc = inWindow[excess] + window;
            if (allowedAtUtc > nowUtc) return LoginLinkErrors.TooManyRequests(LoginCodeIssuer.SecondsUntil(allowedAtUtc, nowUtc));
        }
        foreach (var previous in await links.ListPendingAsync(userId, ct)) previous.Invalidate(nowUtc);
        var token = tokens.Generate();
        var link = LoginLink.Issue(userId, methodId, tokens.Hash(token), nowUtc);
        links.Add(link);
        return new IssuedLoginLink(token, link.ExpiresAtUtc);
    }

    public async Task<Result> InvalidateForMethodAsync(Guid userId, Guid methodId, CancellationToken ct)
    {
        await methods.LockUserAsync(userId, ct);
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        foreach (var link in await links.ListPendingAsync(userId, ct))
            if (link.MethodId == methodId) link.Invalidate(nowUtc);
        return Result.Success();
    }
}
