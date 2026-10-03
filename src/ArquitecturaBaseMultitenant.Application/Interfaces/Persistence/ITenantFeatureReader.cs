using ArquitecturaBaseMultitenant.Application.Models.Features;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Proyección global de habilitaciones de un espacio, sin consultar sus datos privados.</summary>
public interface ITenantFeatureReader
{
    Task<IReadOnlyList<TenantFeatureRow>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
}
