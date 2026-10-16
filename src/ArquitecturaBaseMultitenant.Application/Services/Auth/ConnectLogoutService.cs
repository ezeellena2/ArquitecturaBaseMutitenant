using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>
/// Revoca los tokens de la sesión y la autorización actuales dentro de una transacción para que el logout invalide
/// ambos. Con la sesión de Plataforma (E9), el navegador deja también de tener evidencia del segundo factor.
/// </summary>
internal sealed class ConnectLogoutService(ITokenRevoker tokens, IPlatformFactorRepository platformSessions,
    ISecureTokenGenerator hashes, IUnitOfWork unitOfWork, IInvitationFlowContext invitationFlow, TimeProvider timeProvider,
    ILogger<ConnectLogoutService> logger) : IConnectLogoutService
{
    public Task<Result> RevokeAsync(Guid? userId, string? sessionId,
        string? authorizationId, bool preserveInvitationContinuation, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "RevokeConnectAuthorization", async () =>
        {
            var result = userId is null && string.IsNullOrEmpty(sessionId) && string.IsNullOrEmpty(authorizationId)
                ? Result.Success()
                : await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                if (userId is { } accountId && !string.IsNullOrEmpty(sessionId))
                {
                    await tokens.RevokeSessionAsync(accountId, sessionId, ct);
                    await platformSessions.RevokeSessionAsync(accountId, hashes.Hash(sessionId),
                        timeProvider.GetUtcNow().UtcDateTime, ct);
                }
                if (!string.IsNullOrEmpty(authorizationId))
                {
                    await tokens.RevokeAuthorizationAsync(authorizationId, ct);
                }
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
            if (preserveInvitationContinuation) invitationFlow.PreserveContinuationForAccountSwitch();
            else invitationFlow.ClearContinuation();
            return result;
        });
}
