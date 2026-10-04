using ArquitecturaBaseMultitenant.Domain.Platform;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Prepara asignaciones globales de permisos dentro de una transacción del caso de uso.</summary>
public interface IPlatformPermissionAssignmentRepository
{
    Task<PlatformPermissionAssignment?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    void Add(PlatformPermissionAssignment assignment);
    void Remove(PlatformPermissionAssignment assignment);
}
