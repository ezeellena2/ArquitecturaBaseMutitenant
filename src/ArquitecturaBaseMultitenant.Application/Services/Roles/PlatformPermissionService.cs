using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Roles;

/// <summary>El operador inicial es el Owner de plataforma; su acceso se mantiene en la cuenta global.</summary>
internal sealed class PlatformPermissionService(IUserRepository users) : IPlatformPermissionService
{
    public async Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken);
        return user is { Status: UserStatus.Active, IsPlatformOperator: true }
            ? [.. PlatformPermissions.All]
            : [];
    }

    public async Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken) =>
        (await GetPermissionsAsync(userId, cancellationToken)).Contains(permission, StringComparer.Ordinal);
}
