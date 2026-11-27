namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Consulta la autorización global del operador sin datos de una organización.</summary>
public interface IPlatformPermissionAssignmentReader
{
    /// <summary>Permisos asignados a la cuenta, o null si no tiene asignación.</summary>
    Task<IReadOnlyList<string>?> ListPermissionsAsync(Guid userId, CancellationToken cancellationToken);
}
