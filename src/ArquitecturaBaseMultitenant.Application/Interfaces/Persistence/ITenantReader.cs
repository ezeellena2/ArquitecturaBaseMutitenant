using ArquitecturaBaseMultitenant.Application.Models.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Proyecta espacios globales para verificar su estado y listar empresas activas sin leer filas privadas de cada tenant.</summary>
public interface ITenantReader
{
    Task<TenantRow?> FindByIdAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TenantRow>> ListActiveBusinessesAsync(CancellationToken cancellationToken);

    /// <summary>Todas las organizaciones, en cualquier estado: para la retención, que no depende de que estén activas.</summary>
    Task<IReadOnlyList<TenantRow>> ListBusinessesAsync(CancellationToken cancellationToken);
}
