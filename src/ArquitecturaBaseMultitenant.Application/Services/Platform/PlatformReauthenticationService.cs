using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Idempotency;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>
/// «Confirmá que sos vos» (E9). Con la cuenta bloqueada prueba el código del autenticador o uno de recuperación y deja
/// la prueba reciente en la sesión de Plataforma del token, con el momento del servidor. El consumo, el vínculo
/// durable y la auditoría se confirman juntos; un fallo suma al bloqueo de la cuenta.
/// </summary>
internal sealed class PlatformReauthenticationService(
    ICurrentUser currentUser,
    IPlatformFactorRepository factors,
    TwoFactorCodeVerifier codes,
    PlatformFactorEventWriter events,
    ISignInService signIn,
    IIdempotentRequestContext requests,
    IRequestValidator validator,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<PlatformReauthenticationService> logger) : IPlatformReauthenticationService
{
    public Task<Result<IdempotentReplayResponse<PlatformReauthenticationResponse>>> ReauthenticateAsync(
        PlatformReauthenticationRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<IdempotentReplayResponse<PlatformReauthenticationResponse>>(logger, clock, "PlatformReauthenticate", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (currentUser.Access != Access.Platform || currentUser.UserId is not { } userId
                || currentUser.PlatformSessionId is not { } sessionId)
                return PlatformReauthenticationErrors.SessionEnded;
            return await unitOfWork.ExecuteInTransactionAsync(
                ct => ReauthenticateCoreAsync(userId, sessionId, request, ct), CommitPolicy.OnAnyResult, cancellationToken);
        });

    private async Task<Result<IdempotentReplayResponse<PlatformReauthenticationResponse>>> ReauthenticateCoreAsync(
        Guid userId, Guid sessionId, PlatformReauthenticationRequest request, CancellationToken ct)
    {
        await factors.LockAccountAsync(userId, ct);
        var session = await factors.GetSessionAsync(sessionId, ct);
        if (session is null || session.UserId != userId
            || !session.IsValid(await signIn.GetSecurityStampAsync(userId, ct)))
            return PlatformReauthenticationErrors.SessionEnded;
        var proved = await codes.VerifyAsync(userId, request.Method!.Value, request.Code!, ct);
        if (proved.IsFailure) return proved.Error;

        // En microsegundos, la precisión de la base: un replay devuelve exactamente los mismos instantes.
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        nowUtc = nowUtc.AddTicks(-(nowUtc.Ticks % TimeSpan.TicksPerMicrosecond));
        session.ConfirmRecentProof(nowUtc);
        await signIn.ResetFailedAttemptsAsync(userId, ct);
        events.RecordReauthenticated(userId);
        if (proved.Value == LoginAuditMethod.RecoveryCode) events.RecordRecoveryCodeUsed(userId);
        var durable = requests.Request ?? throw new InvalidOperationException("A durable request is required.");
        var operation = PlatformFactorOperation.ForReauthentication(durable.Key, userId, session.Id, durable.BodyHash,
            durable.Route, session.RecentProofAtUtc, session.RecentProofUntilUtc, nowUtc);
        factors.Add(operation);
        return new IdempotentReplayResponse<PlatformReauthenticationResponse>(
            PlatformFactorReferences.For(PlatformFactorReferences.Reauthentication, operation.Id),
            new PlatformReauthenticationResponse(session.RecentProofAtUtc, session.RecentProofUntilUtc));
    }
}
