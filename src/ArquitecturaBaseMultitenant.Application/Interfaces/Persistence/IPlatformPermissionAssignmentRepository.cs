using ArquitecturaBaseMultitenant.Domain.Platform;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Prepara asignaciones globales de permisos dentro de una transacción del caso de uso.</summary>
public interface IPlatformPermissionAssignmentRepository
{
    Task<PlatformPermissionAssignment?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    void Add(PlatformPermissionAssignment assignment);
    void Remove(PlatformPermissionAssignment assignment);

    /// <summary>Lock global: quitar, suspender o dar de baja a un operador se serializa con este lock.</summary>
    Task LockOperatorsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Operadores activos con <c>platform.operators.manage</c>, leídos dentro de la transacción; una invitación sin
    /// aceptar no cuenta.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListActiveManagerIdsAsync(CancellationToken cancellationToken);
}
