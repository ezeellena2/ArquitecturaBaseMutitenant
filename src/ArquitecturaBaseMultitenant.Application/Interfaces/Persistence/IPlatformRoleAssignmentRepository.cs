using ArquitecturaBaseMultitenant.Domain.Platform;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Prepara asignaciones globales dentro de una transacción del caso de uso.</summary>
public interface IPlatformRoleAssignmentRepository
{
    Task<PlatformRoleAssignment?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    void Add(PlatformRoleAssignment assignment);
    void Remove(PlatformRoleAssignment assignment);
}
