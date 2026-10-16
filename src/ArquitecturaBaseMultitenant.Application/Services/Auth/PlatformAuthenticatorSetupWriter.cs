using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Idempotency;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>
/// El alta del autenticador dentro de la transacción del segundo factor (E9). Al verificar la clave nueva genera los
/// ocho códigos de recuperación y los deja protegidos sólo en el desafío; al completar guarda la clave y únicamente los
/// hashes de los códigos, y registra el alta.
/// </summary>
internal sealed class PlatformAuthenticatorSetupWriter(IPlatformFactorRepository factors, IPlatformFactorSecrets secrets,
    PlatformFactorEventWriter events, IIdempotentRequestContext requests, TimeProvider clock)
{
    /// <summary>La clave quedó verificada: devuelve el vínculo durable de la respuesta que pide mostrar los códigos.</summary>
    internal Result<Guid> ConfirmSetup(PlatformFactorChallenge challenge, long acceptedTimeStep)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        var confirmed = challenge.ConfirmSetup(acceptedTimeStep,
            secrets.ProtectRecoveryCodes(challenge.Id, secrets.NewRecoveryCodes()));
        if (confirmed.IsFailure) return confirmed.Error;
        var request = requests.Request ?? throw new InvalidOperationException("A durable request is required.");
        var operation = PlatformFactorOperation.ForSetupVerified(request.Key, challenge.UserId, challenge.Id,
            request.BodyHash, request.Route, clock.GetUtcNow().UtcDateTime);
        factors.Add(operation);
        return operation.Id;
    }

    /// <summary>Los códigos quedaron guardados: el autenticador nace con el intervalo de su verificación.</summary>
    internal async Task<Result> ConfigureAsync(PlatformFactorChallenge challenge, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        if (challenge is not { Step: PlatformFactorStep.RecoveryCodes, ProtectedSecret: { } secret,
                ProtectedRecoveryCodes: { } codes, AcceptedTimeStep: { } acceptedTimeStep }
            || await factors.GetAuthenticatorAsync(challenge.UserId, ct) is not null)
            return TwoFactorErrors.InvalidState;
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        factors.Add(PlatformAuthenticator.Configure(challenge.UserId, secret, acceptedTimeStep, nowUtc));
        factors.Add(secrets.UnprotectRecoveryCodes(challenge.Id, codes)
            .Select(code => PlatformRecoveryCode.Issue(challenge.UserId, secrets.HashRecoveryCode(challenge.UserId, code), nowUtc))
            .ToArray());
        events.RecordConfigured(challenge.UserId);
        return Result.Success();
    }
}
