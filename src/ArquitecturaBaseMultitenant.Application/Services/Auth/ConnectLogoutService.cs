using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

internal sealed class ConnectLogoutService(ITokenRevoker tokens, IUnitOfWork unitOfWork,
    TimeProvider timeProvider, ILogger<ConnectLogoutService> logger) : IConnectLogoutService
{
    public Task<Result> RevokeAsync(Guid? userId, string? sessionId,
        string? authorizationId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "RevokeConnectAuthorization", () =>
        {
            return unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                if (userId is { } accountId && !string.IsNullOrEmpty(sessionId))
                {
                    await tokens.RevokeSessionAsync(accountId, sessionId, ct);
                }
                if (!string.IsNullOrEmpty(authorizationId))
                {
                    await tokens.RevokeAuthorizationAsync(authorizationId, ct);
                }
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
        });
}
