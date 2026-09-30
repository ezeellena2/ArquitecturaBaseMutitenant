using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

internal sealed class ConnectAuthorizationService(
    IConnectAuthorizationStore store, IUnitOfWork unitOfWork,
    TimeProvider timeProvider, ILogger<ConnectAuthorizationService> logger) : IConnectAuthorizationService
{
    public Task<Result<string>> CreateAsync(Guid userId, Access access, Guid? tenantId,
        string sessionId, string clientId, IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "CreateConnectAuthorization", () =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
            return unitOfWork.ExecuteInTransactionAsync(async ct =>
                Result.Success(await store.CreateAsync(userId, access, tenantId,
                    sessionId, clientId, scopes, ct)), CommitPolicy.OnSuccess, cancellationToken);
        });
}
