using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>
/// Segundo factor del ingreso de un operador (E9). El GET lee el desafío del navegador sin escribir. Verificar y
/// completar toman la cuenta y el desafío bajo lock en una UoW: el consumo del código, el vínculo durable, la sesión y
/// la auditoría se confirman juntos, y la cookie de Plataforma sale recién después del commit.
/// </summary>
internal sealed class PlatformFactorService(
    PlatformFactorChallengeResolver resolver,
    PlatformFactorChallengeGuard guard,
    TwoFactorCodeVerifier codes,
    PlatformAuthenticatorSetupWriter setup,
    PlatformSignInWriter signIns,
    IPlatformFactorSecrets secrets,
    IPlatformFactorBrowser browser,
    ISignInService signIn,
    IRequestValidator validator,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<PlatformFactorService> logger) : IPlatformFactorService
{
    public Task<Result<PlatformFactorChallengeResponse>> GetAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PlatformFactorChallengeResponse>(logger, clock, "PlatformFactorGet", async () =>
        {
            var resolved = await resolver.ResolveAsync(cancellationToken);
            if (resolved.IsFailure) return resolved.Error;
            var (_, challenge, account) = resolved.Value;
            switch (challenge.Step)
            {
                case PlatformFactorStep.Verify:
                    return new PlatformFactorChallengeResponse(PlatformFactorState.Verify, challenge.ExpiresAtUtc);
                case PlatformFactorStep.Setup:
                    var described = secrets.DescribeSetup(account.Id, challenge.ProtectedSecret!,
                        account.PrimaryEmail?.Value ?? account.Id.ToString("D"));
                    return new PlatformFactorChallengeResponse(PlatformFactorState.Setup, challenge.ExpiresAtUtc,
                        described.Secret, described.OtpauthUri);
                default:
                    return new PlatformFactorChallengeResponse(PlatformFactorState.RecoveryCodes, challenge.ExpiresAtUtc,
                        RecoveryCodes: secrets.UnprotectRecoveryCodes(challenge.Id, challenge.ProtectedRecoveryCodes!));
            }
        });

    public Task<Result<IdempotentReplayResponse<VerifyPlatformFactorResponse>>> VerifyAsync(
        VerifyPlatformFactorRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<IdempotentReplayResponse<VerifyPlatformFactorResponse>>(logger, clock, "PlatformFactorVerify", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (browser.Read() is not { } binding) return TwoFactorErrors.ChallengeExpired;
            // Los fallos también se confirman: el intento suma al bloqueo y queda en la auditoría.
            var verified = await unitOfWork.ExecuteInTransactionAsync(
                ct => VerifyCoreAsync(binding, request, ct), CommitPolicy.OnAnyResult, cancellationToken);
            if (verified.IsFailure) return verified.Error;
            if (verified.Value.SignIn is { } signedIn)
            {
                await EmitSessionAsync(signedIn, cancellationToken);
                return new IdempotentReplayResponse<VerifyPlatformFactorResponse>(
                    PlatformFactorReferences.For(PlatformFactorReferences.Verify, signedIn.OperationId),
                    new VerifyPlatformFactorResponse(PlatformFactorNextStep.Authorize, signedIn.ReturnUrl));
            }
            return new IdempotentReplayResponse<VerifyPlatformFactorResponse>(
                PlatformFactorReferences.For(PlatformFactorReferences.Verify, verified.Value.OperationId),
                new VerifyPlatformFactorResponse(PlatformFactorNextStep.RecoveryCodes));
        });

    public Task<Result<IdempotentReplayResponse<CompletePlatformFactorResponse>>> CompleteAsync(
        CompletePlatformFactorRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<IdempotentReplayResponse<CompletePlatformFactorResponse>>(logger, clock, "PlatformFactorComplete", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (browser.Read() is not { } binding) return TwoFactorErrors.ChallengeExpired;
            var completed = await unitOfWork.ExecuteInTransactionAsync(
                ct => CompleteCoreAsync(binding, ct), CommitPolicy.OnSuccess, cancellationToken);
            if (completed.IsFailure) return completed.Error;
            await EmitSessionAsync(completed.Value, cancellationToken);
            return new IdempotentReplayResponse<CompletePlatformFactorResponse>(
                PlatformFactorReferences.For(PlatformFactorReferences.Complete, completed.Value.OperationId),
                new CompletePlatformFactorResponse(completed.Value.ReturnUrl));
        });

    private async Task<Result<VerifiedPlatformFactor>> VerifyCoreAsync(PlatformFactorBinding binding,
        VerifyPlatformFactorRequest request, CancellationToken ct)
    {
        var guarded = await guard.LockAsync(binding, ct);
        if (guarded.IsFailure) return guarded.Error;
        var challenge = guarded.Value.Challenge;
        var method = request.Method!.Value;
        if (challenge.Step == PlatformFactorStep.Setup)
        {
            // El alta se prueba sólo con el autenticador: todavía no hay códigos de recuperación.
            if (method != TwoFactorMethod.Authenticator) return TwoFactorErrors.InvalidState;
            var step = await codes.VerifySetupAsync(challenge.UserId, challenge.ProtectedSecret!, request.Code!, ct);
            if (step.IsFailure) return step.Error;
            var confirmed = setup.ConfirmSetup(challenge, step.Value);
            if (confirmed.IsFailure) return confirmed.Error;
            return new VerifiedPlatformFactor(confirmed.Value, null);
        }
        if (challenge.Step != PlatformFactorStep.Verify) return TwoFactorErrors.InvalidState;
        var proved = await codes.VerifyAsync(challenge.UserId, method, request.Code!, ct);
        if (proved.IsFailure) return proved.Error;
        var signedIn = await signIns.SignInAsync(guarded.Value, PlatformFactorStep.Verify,
            PlatformFactorOperationKind.Verify, proved.Value, ct);
        if (signedIn.IsFailure) return signedIn.Error;
        return new VerifiedPlatformFactor(signedIn.Value.OperationId, signedIn.Value);
    }

    private async Task<Result<PlatformSignIn>> CompleteCoreAsync(PlatformFactorBinding binding, CancellationToken ct)
    {
        var guarded = await guard.LockAsync(binding, ct);
        if (guarded.IsFailure) return guarded.Error;
        var configured = await setup.ConfigureAsync(guarded.Value.Challenge, ct);
        if (configured.IsFailure) return configured.Error;
        return await signIns.SignInAsync(guarded.Value, PlatformFactorStep.RecoveryCodes,
            PlatformFactorOperationKind.Complete, LoginAuditMethod.Authenticator, ct);
    }

    /// <summary>Después del commit: la cookie de Plataforma y el retiro de la cookie del desafío.</summary>
    private async Task EmitSessionAsync(PlatformSignIn signedIn, CancellationToken ct)
    {
        await signIn.SignInPlatformAsync(signedIn.Session.UserId, signedIn.Session.SessionId,
            signedIn.Session.StartedAtUtc, ct);
        browser.Clear();
    }

    private sealed record VerifiedPlatformFactor(Guid OperationId, PlatformSignIn? SignIn);
}
