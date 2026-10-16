using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>
/// Protege el desafío pendiente en una escritura del segundo factor (E9). Localiza la cuenta con una lectura sin
/// tracking, toma el lock de la cuenta y el de la fila, y relee: exige el mismo navegador, el desafío vigente y la
/// cuenta operadora activa con el sello con que nació. Cualquier diferencia responde como un desafío vencido.
/// </summary>
internal sealed class PlatformFactorChallengeGuard(IPlatformFactorReader reader, IPlatformFactorRepository factors,
    IUserRepository users, ISecureTokenGenerator tokens, ISignInService signIn, TimeProvider clock)
{
    internal async Task<Result<GuardedPlatformFactor>> LockAsync(PlatformFactorBinding binding, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (await reader.FindChallengeAsync(binding.ChallengeId, ct) is not { } hint) return TwoFactorErrors.ChallengeExpired;
        await factors.LockAccountAsync(hint.UserId, ct);
        var challenge = await factors.GetChallengeForUpdateAsync(binding.ChallengeId, ct);
        if (challenge is null || challenge.UserId != hint.UserId
            || !string.Equals(challenge.BindingHash, tokens.Hash(binding.Token), StringComparison.Ordinal)
            || !challenge.IsActive(clock.GetUtcNow().UtcDateTime))
            return TwoFactorErrors.ChallengeExpired;
        var account = await users.GetByIdAsync(challenge.UserId, ct);
        if (account is not { Status: UserStatus.Active, IsPlatformOperator: true }
            || !string.Equals(await signIn.GetSecurityStampAsync(challenge.UserId, ct), challenge.SecurityStamp, StringComparison.Ordinal))
            return TwoFactorErrors.ChallengeExpired;
        return new GuardedPlatformFactor(challenge, account);
    }
}

/// <summary>El desafío bloqueado y la cuenta operadora que lo dueña, dentro de la transacción del servicio.</summary>
internal sealed record GuardedPlatformFactor(PlatformFactorChallenge Challenge, UserAccountRow Account);
