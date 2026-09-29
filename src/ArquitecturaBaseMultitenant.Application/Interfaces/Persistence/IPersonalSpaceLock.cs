namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Serializa la creación del espacio personal de una identidad entre réplicas.</summary>
public interface IPersonalSpaceLock
{
    Task LockAsync(Guid userId, CancellationToken cancellationToken);
}
