using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>
/// Lee el desafío pendiente del navegador sin escribir (E9): el GET y el replay lo consultan con las mismas reglas
/// que la guarda de las escrituras, sin locks.
/// </summary>
internal sealed class PlatformFactorChallengeResolver(IPlatformFactorBrowser browser, IPlatformFactorReader reader,
    IUserRepository users, ISecureTokenGenerator tokens, ISignInService signIn, TimeProvider clock)
{
    internal async Task<Result<ResolvedPlatformFactor>> ResolveAsync(CancellationToken ct, bool requireActive = true)
    {
        if (browser.Read() is not { } binding) return TwoFactorErrors.ChallengeExpired;
        var challenge = await reader.FindChallengeAsync(binding.ChallengeId, ct);
        if (challenge is null || !string.Equals(challenge.BindingHash, tokens.Hash(binding.Token), StringComparison.Ordinal)
            || requireActive && !challenge.IsActive(clock.GetUtcNow().UtcDateTime))
            return TwoFactorErrors.ChallengeExpired;
        var account = await users.GetByIdAsync(challenge.UserId, ct);
        if (account is not { Status: UserStatus.Active, IsPlatformOperator: true }
            || !string.Equals(await signIn.GetSecurityStampAsync(challenge.UserId, ct), challenge.SecurityStamp, StringComparison.Ordinal))
            return TwoFactorErrors.ChallengeExpired;
        return new ResolvedPlatformFactor(binding, challenge, account);
    }
}

/// <summary>El vínculo del navegador, su desafío y la cuenta operadora, leídos sin tracking.</summary>
internal sealed record ResolvedPlatformFactor(PlatformFactorBinding Binding, PlatformFactorChallengeRow Challenge,
    UserAccountRow Account);
