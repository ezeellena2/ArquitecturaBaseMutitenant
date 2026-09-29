using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IConnectService
{
    Task<Result<ConnectUser>> GetActiveUserAsync(Guid userId, Access access,
        Guid? requestedTenantId, CancellationToken cancellationToken);

    Task<Result<ConnectUser>> SelectAccessAsync(Guid userId, Access access,
        Guid? requestedTenantId, CancellationToken cancellationToken);
}
