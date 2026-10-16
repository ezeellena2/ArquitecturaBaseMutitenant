using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>
/// Protocolo durable del segundo factor (E9), como el del Asistente. Antes de reservar autoriza con el desafío del
/// navegador o con la sesión del token y busca el vínculo de la clave: la misma operación se repite, otra ruta o cuerpo
/// es 422. El replay revalida cuenta, sello, acceso y prueba, y recién entonces reconstruye la respuesta; la de un
/// ingreso vuelve a emitir la cookie de la misma sesión y la de una reautenticación conserva su vencimiento original.
/// </summary>
internal sealed class PlatformFactorDurableService(PlatformFactorChallengeResolver challenges, IPlatformFactorReader reader,
    IPlatformFactorSecrets secrets, ISignInService signIn, IPlatformFactorBrowser browser, ICurrentUser currentUser,
    TimeProvider clock, ILogger<PlatformFactorDurableService> logger) : IPlatformFactorDurableService
{
    public Task<Result<IdempotentReference?>> ValidateFactorAsync(IdempotentRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<IdempotentReference?>(logger, clock, "PlatformFactorDurableValidate", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var resolved = await challenges.ResolveAsync(ct, requireActive: false);
            if (resolved.IsFailure) return resolved.Error;
            var operation = await reader.FindOperationAsync(request.Key, ct);
            if (operation is null) return Result.Success<IdempotentReference?>(null);
            if (operation.Kind == PlatformFactorOperationKind.Reauthenticate || operation.ChallengeId != resolved.Value.Challenge.Id
                || !operation.Matches(request.BodyHash, request.Route))
                return RequestErrors.IdempotencyKeyReused;
            return Reference(operation);
        });

    public Task<Result<IdempotentReference?>> ValidateReauthenticationAsync(IdempotentRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<IdempotentReference?>(logger, clock, "PlatformReauthenticationDurableValidate", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var session = await CurrentSessionAsync(ct);
            if (session.IsFailure) return session.Error;
            var operation = await reader.FindOperationAsync(request.Key, ct);
            if (operation is null) return Result.Success<IdempotentReference?>(null);
            if (operation.Kind != PlatformFactorOperationKind.Reauthenticate || operation.PlatformSessionId != session.Value.Id
                || !operation.Matches(request.BodyHash, request.Route))
                return RequestErrors.IdempotencyKeyReused;
            return Reference(operation);
        });

    public Task<Result<IdempotentReplayResponse>> ReplayAsync(IdempotentReference reference, CancellationToken ct) =>
        OperationLog.RunAsync<IdempotentReplayResponse>(logger, clock, "PlatformFactorDurableReplay", async () =>
        {
            ArgumentNullException.ThrowIfNull(reference);
            var operation = await reader.FindOperationByIdAsync(reference.Id, ct);
            if (operation is null || Reference(operation) != reference) return TwoFactorErrors.ChallengeExpired;
            if (operation.Kind == PlatformFactorOperationKind.Reauthenticate)
                return await ReplayReauthenticationAsync(reference, operation, ct);
            return await ReplaySignInAsync(reference, operation, ct);
        });

    private async Task<Result<IdempotentReplayResponse>> ReplaySignInAsync(IdempotentReference reference,
        PlatformFactorOperationRow operation, CancellationToken ct)
    {
        var resolved = await challenges.ResolveAsync(ct, requireActive: operation.ShowsRecoveryCodes);
        if (resolved.IsFailure) return resolved.Error;
        var challenge = resolved.Value.Challenge;
        if (operation.ChallengeId != challenge.Id || operation.UserId != challenge.UserId) return TwoFactorErrors.ChallengeExpired;
        if (operation.ShowsRecoveryCodes)
        {
            if (challenge.Step != PlatformFactorStep.RecoveryCodes) return TwoFactorErrors.ChallengeExpired;
            return new IdempotentReplayResponse<VerifyPlatformFactorResponse>(reference,
                new VerifyPlatformFactorResponse(PlatformFactorNextStep.RecoveryCodes));
        }

        var session = operation.PlatformSessionId is { } id ? await reader.FindSessionAsync(id, ct) : null;
        if (session is null || operation.ProtectedSessionId is not { } protectedSessionId || operation.ReturnUrl is not { } returnUrl
            || !session.IsValid(await signIn.GetSecurityStampAsync(operation.UserId, ct)))
            return TwoFactorErrors.ChallengeExpired;
        await signIn.SignInPlatformAsync(operation.UserId, secrets.UnprotectSessionId(session.Id, protectedSessionId),
            session.StartedAtUtc, ct);
        browser.Clear();
        if (operation.Kind == PlatformFactorOperationKind.Complete)
            return new IdempotentReplayResponse<CompletePlatformFactorResponse>(reference, new CompletePlatformFactorResponse(returnUrl));
        return new IdempotentReplayResponse<VerifyPlatformFactorResponse>(reference,
            new VerifyPlatformFactorResponse(PlatformFactorNextStep.Authorize, returnUrl));
    }

    private async Task<Result<IdempotentReplayResponse>> ReplayReauthenticationAsync(IdempotentReference reference,
        PlatformFactorOperationRow operation, CancellationToken ct)
    {
        var session = await CurrentSessionAsync(ct);
        if (session.IsFailure) return session.Error;
        if (operation.PlatformSessionId != session.Value.Id || operation is not { VerifiedAtUtc: { } verifiedAtUtc, RecentUntilUtc: { } untilUtc })
            return PlatformReauthenticationErrors.SessionEnded;
        return new IdempotentReplayResponse<PlatformReauthenticationResponse>(reference,
            new PlatformReauthenticationResponse(verifiedAtUtc, untilUtc));
    }

    /// <summary>La sesión de Plataforma del token, todavía válida para su cuenta.</summary>
    private async Task<Result<PlatformSessionRow>> CurrentSessionAsync(CancellationToken ct)
    {
        if (currentUser.Access != Access.Platform || currentUser.UserId is not { } userId
            || currentUser.PlatformSessionId is not { } sessionId
            || await reader.FindSessionAsync(sessionId, ct) is not { } session || session.UserId != userId
            || !session.IsValid(await signIn.GetSecurityStampAsync(userId, ct)))
            return PlatformReauthenticationErrors.SessionEnded;
        return session;
    }

    private static IdempotentReference Reference(PlatformFactorOperationRow operation) => PlatformFactorReferences.For(
        operation.Kind switch
        {
            PlatformFactorOperationKind.Verify => PlatformFactorReferences.Verify,
            PlatformFactorOperationKind.Complete => PlatformFactorReferences.Complete,
            _ => PlatformFactorReferences.Reauthentication,
        }, operation.Id);
}
