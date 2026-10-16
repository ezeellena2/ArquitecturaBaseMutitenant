using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>
/// Prueba el segundo factor de un operador dentro de la transacción del llamador, con la cuenta bloqueada (E9). El
/// código del autenticador consume su intervalo y uno de recuperación queda usado. Un fallo suma al contador de la
/// cuenta, el mismo del primer factor, y queda en la auditoría de ingreso; con la cuenta bloqueada no prueba nada e
/// informa cuánto falta.
/// </summary>
internal sealed class TwoFactorCodeVerifier(IPlatformFactorRepository factors, IPlatformFactorSecrets secrets,
    ISignInService signIn, ILoginAuditRepository audits, TimeProvider clock)
{
    /// <summary>Con el autenticador ya configurado: su código o uno de recuperación.</summary>
    internal async Task<Result<LoginAuditMethod>> VerifyAsync(Guid userId, TwoFactorMethod method, string code,
        CancellationToken ct)
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        if (await LockedOutAsync(userId, nowUtc, ct) is { } locked) return locked;
        var auditMethod = method == TwoFactorMethod.RecoveryCode ? LoginAuditMethod.RecoveryCode : LoginAuditMethod.Authenticator;
        var proved = method == TwoFactorMethod.RecoveryCode
            ? await UseRecoveryCodeAsync(userId, code, nowUtc, ct)
            : await AcceptAuthenticatorAsync(userId, code, nowUtc, ct);
        if (proved.IsSuccess) return auditMethod;
        return await FailAsync(userId, auditMethod, proved.Error, ct);
    }

    /// <summary>En el alta: el código de la clave nueva del desafío. Devuelve su intervalo, que ya no vuelve a servir.</summary>
    internal async Task<Result<long>> VerifySetupAsync(Guid userId, byte[] protectedSecret, string code,
        CancellationToken ct)
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        if (await LockedOutAsync(userId, nowUtc, ct) is { } locked) return locked;
        if (secrets.MatchTimeStep(userId, protectedSecret, code, nowUtc) is { } step) return step;
        return await FailAsync(userId, LoginAuditMethod.Authenticator, TwoFactorErrors.Invalid, ct);
    }

    private async Task<Result> AcceptAuthenticatorAsync(Guid userId, string code, DateTime nowUtc, CancellationToken ct)
    {
        var authenticator = await factors.GetAuthenticatorAsync(userId, ct);
        if (authenticator is null) return TwoFactorErrors.Invalid;
        return secrets.MatchTimeStep(userId, authenticator.ProtectedSecret, code, nowUtc) is { } step
            ? authenticator.Accept(step)
            : TwoFactorErrors.Invalid;
    }

    private async Task<Result> UseRecoveryCodeAsync(Guid userId, string code, DateTime nowUtc, CancellationToken ct)
    {
        if (TwoFactorCodeFormat.NormalizeRecoveryCode(code) is not { } normalized) return TwoFactorErrors.RecoveryInvalid;
        var stored = await factors.GetRecoveryCodeAsync(userId, secrets.HashRecoveryCode(userId, normalized), ct);
        return stored is null ? TwoFactorErrors.RecoveryInvalid : stored.Use(nowUtc);
    }

    /// <summary>El fallo cuenta como cualquier código incorrecto; el que completa los diez ya responde el bloqueo.</summary>
    private async Task<Error> FailAsync(Guid userId, LoginAuditMethod method, Error error, CancellationToken ct)
    {
        await signIn.RegisterFailedAttemptAsync(userId, ct);
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        audits.Add(LoginAudit.Failure(userId, method, error, nowUtc));
        return await LockedOutAsync(userId, nowUtc, ct) ?? FieldErrors.On(error, TwoFactorErrors.CodeField);
    }

    private async Task<Error?> LockedOutAsync(Guid userId, DateTime nowUtc, CancellationToken ct) =>
        await signIn.GetLockoutEndUtcAsync(userId, ct) is { } untilUtc
            ? AccountErrors.LockedOutFor((int)Math.Ceiling((untilUtc - nowUtc).TotalSeconds))
            : null;
}
