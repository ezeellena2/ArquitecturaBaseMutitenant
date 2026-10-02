using ArquitecturaBaseMultitenant.Application.Models.Roles;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Proyecta roles de la organización activa para el detalle y los selectores de autorización.</summary>
public interface IRoleReader
{
    Task<IReadOnlyList<RoleRow>> ListAllAsync(CancellationToken cancellationToken);

    Task<RoleRow?> FindByIdAsync(Guid roleId, CancellationToken cancellationToken);

    Task<bool> ExistsByNameAsync(string name, Guid? excludedRoleId, CancellationToken cancellationToken);
}
