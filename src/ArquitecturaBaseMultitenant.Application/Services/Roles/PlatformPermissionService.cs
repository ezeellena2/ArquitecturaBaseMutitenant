using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Roles;

/// <summary>Resuelve los permisos persistidos de operadores activos, sin conceder permisos por el indicador de acceso.</summary>
internal sealed class PlatformPermissionService(IUserRepository users, IPlatformPermissionAssignmentReader assignments,
    TimeProvider timeProvider, ILogger<PlatformPermissionService> logger) : IPlatformPermissionService
{
    public async Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var result = await OperationLog.RunAsync(logger, timeProvider, "GetPlatformPermissions",
            () => ReadAsync(userId, cancellationToken));
        return result.Value;
    }

    public async Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken)
    {
        var result = await OperationLog.RunAsync(logger, timeProvider, "HasPlatformPermission", async () =>
        {
            var permissions = await ReadAsync(userId, cancellationToken);
            return Result.Success(permissions.Value.Contains(permission, StringComparer.Ordinal));
        });
        return result.Value;
    }

    private async Task<Result<IReadOnlyList<string>>> ReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken);
        // ADR 0042: una invitación sin aceptar todavía no da permisos.
        if (user is not { Status: UserStatus.Active, IsPlatformOperator: true, HasPendingOperatorInvitation: false })
            return Result.Success<IReadOnlyList<string>>([]);
        var permissions = await assignments.FindPermissionsAsync(userId, cancellationToken);
        return Result.Success(permissions ?? []);
    }
}
