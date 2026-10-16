using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>
/// La sesión de Plataforma como evidencia del segundo factor (E9), leída siempre de la base y nunca de una caché. El
/// authorize y el refresh del operador entregan tokens sólo con una sesión vigente y con el mismo sello; una acción
/// sensible exige además la prueba reciente de esa sesión y la deja pedida para la UoW que escribe.
/// </summary>
internal sealed class PlatformSessionService(IPlatformFactorReader reader, ISecureTokenGenerator tokens,
    ISignInService signIn, ICurrentUser currentUser, IRecentPlatformProofContext proofs, TimeProvider clock,
    ILogger<PlatformSessionService> logger) : IPlatformSessionService
{
    public Task<Result<Guid>> FindValidSessionAsync(Guid userId, string browserSessionId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<Guid>(logger, clock, "FindPlatformSession", async () =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(browserSessionId);
            var session = await reader.FindSessionByHashAsync(userId, tokens.Hash(browserSessionId), cancellationToken);
            if (session is null || !session.IsValid(await signIn.GetSecurityStampAsync(userId, cancellationToken)))
                return PlatformReauthenticationErrors.SessionEnded;
            return session.Id;
        });

    public Task<Result> CheckSessionAsync(Guid userId, Guid platformSessionId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "CheckPlatformSession", async () =>
        {
            var session = await reader.FindSessionAsync(platformSessionId, cancellationToken);
            return session is not null && session.UserId == userId
                && session.IsValid(await signIn.GetSecurityStampAsync(userId, cancellationToken))
                ? Result.Success() : Result.Failure(PlatformReauthenticationErrors.SessionEnded);
        });

    public Task<Result> RequireRecentProofAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "RequireRecentPlatformProof", async () =>
        {
            if (currentUser.Access != Access.Platform || currentUser.UserId is not { } userId
                || currentUser.PlatformSessionId is not { } sessionId
                || await reader.FindSessionAsync(sessionId, cancellationToken) is not { } session || session.UserId != userId
                || !session.IsValid(await signIn.GetSecurityStampAsync(userId, cancellationToken))
                || clock.GetUtcNow().UtcDateTime >= session.RecentProofUntilUtc)
                return Result.Failure(PlatformReauthenticationErrors.Required);
            proofs.Require(session.Id);
            return Result.Success();
        });
}
