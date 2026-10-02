namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Serializa cambios de asignaciones Dueño con el chequeo de baja del último Dueño.</summary>
public interface ITenantAdminLock
{
    Task LockAssignmentsAsync(Guid tenantId, CancellationToken ct);
}
