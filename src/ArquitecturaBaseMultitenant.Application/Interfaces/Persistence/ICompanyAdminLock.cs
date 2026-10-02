namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Serializa cambios de asignaciones de Administrador de la misma empresa.</summary>
public interface ICompanyAdminLock
{
    Task LockAssignmentsAsync(Guid companyId, CancellationToken cancellationToken);
}
