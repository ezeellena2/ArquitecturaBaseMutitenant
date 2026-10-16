using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>
/// Después del primer factor de un operador con access=platform (E9, ADR 0043): dentro de la misma transacción
/// invalida sus desafíos anteriores y emite uno nuevo, ligado al navegador, al sello de la cuenta y al authorize
/// validado. Sin autenticador prepara la clave del alta. Nunca crea la sesión ni acepta la invitación.
/// </summary>
internal sealed class PlatformFactorChallengeIssuer(IPlatformFactorRepository factors, IPlatformFactorSecrets secrets,
    ISecureTokenGenerator tokens, ISignInService signIn, TimeProvider clock)
{
    internal async Task<PlatformFactorBinding> IssueAsync(Guid userId, string returnUrl, CancellationToken ct)
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        await factors.InvalidateChallengesAsync(userId, nowUtc, ct);
        var token = tokens.Generate();
        var stamp = await signIn.GetSecurityStampAsync(userId, ct);
        var challenge = await factors.GetAuthenticatorAsync(userId, ct) is null
            ? PlatformFactorChallenge.ForSetup(userId, tokens.Hash(token), stamp, returnUrl,
                secrets.NewProtectedSecret(userId), nowUtc)
            : PlatformFactorChallenge.ForVerify(userId, tokens.Hash(token), stamp, returnUrl, nowUtc);
        factors.Add(challenge);
        return new PlatformFactorBinding(challenge.Id, token);
    }
}
