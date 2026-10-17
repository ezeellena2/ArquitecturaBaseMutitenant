using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Idempotency;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>
/// Termina el ingreso de un operador dentro de la transacción del segundo factor (E9): acepta la invitación pendiente,
/// completa el desafío, crea la sesión de Plataforma, borra los fallos y registra el ingreso con el método usado. El
/// vínculo durable con la clave de idempotencia se confirma junto con todo eso; la cookie sale después del commit. El
/// ingreso con un código de recuperación queda sólo como ese ingreso: una fila por acción, como Auditoría (E9).
/// </summary>
internal sealed class PlatformSignInWriter(IPlatformFactorRepository factors, IPlatformFactorSecrets secrets,
    IUserRepository users, ISignInService signIn, ILoginAuditRepository audits,
    ISecureTokenGenerator tokens, IIdempotentRequestContext requests, TimeProvider clock)
{
    internal async Task<Result<PlatformSignIn>> SignInAsync(GuardedPlatformFactor guarded, PlatformFactorStep expected,
        PlatformFactorOperationKind kind, LoginAuditMethod method, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(guarded);
        var (challenge, account) = guarded;
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        if (account.HasPendingOperatorInvitation)
        {
            // ADR 0042: la invitación queda aceptada recién al completar los dos factores.
            if (account.OperatorInvitationExpiresAtUtc <= nowUtc) return TwoFactorErrors.ChallengeExpired;
            var accepted = await users.AcceptOperatorInvitationAsync(account.Id, nowUtc, ct);
            if (accepted.IsFailure) return accepted.Error;
        }
        var completed = challenge.Complete(expected, nowUtc);
        if (completed.IsFailure) return completed.Error;

        var sessionId = tokens.Generate();
        var session = PlatformSession.Start(account.Id, tokens.Hash(sessionId),
            await signIn.GetSecurityStampAsync(account.Id, ct), method, nowUtc);
        factors.Add(session);
        await signIn.ResetFailedAttemptsAsync(account.Id, ct);
        audits.Add(LoginAudit.Success(account.Id, method, nowUtc));

        var request = requests.Request ?? throw new InvalidOperationException("A durable request is required.");
        var operation = PlatformFactorOperation.ForSignIn(request.Key, kind, account.Id, challenge.Id, request.BodyHash,
            request.Route, challenge.ReturnUrl, session.Id, secrets.ProtectSessionId(session.Id, sessionId), nowUtc);
        factors.Add(operation);
        return new PlatformSignIn(operation.Id, challenge.ReturnUrl, new IssuedPlatformSession(session.Id, account.Id, sessionId, nowUtc));
    }
}

/// <summary>El ingreso confirmado: el vínculo durable, el retorno validado y la sesión que se emite después del commit.</summary>
internal sealed record PlatformSignIn(Guid OperationId, string ReturnUrl, IssuedPlatformSession Session);
