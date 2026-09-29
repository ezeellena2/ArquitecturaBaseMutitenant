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
    public Task<Result> RevokeAuthorizationAsync(string authorizationId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "RevokeConnectAuthorization", () =>
        {
            ArgumentException.ThrowIfNullOrEmpty(authorizationId);
            return unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await tokens.RevokeAuthorizationAsync(authorizationId, ct);
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
        });
}
