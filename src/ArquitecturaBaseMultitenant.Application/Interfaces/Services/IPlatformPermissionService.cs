namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Consulta permisos del acceso de operador, independiente de los tenants.</summary>
public interface IPlatformPermissionService
{
    Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken);
}
