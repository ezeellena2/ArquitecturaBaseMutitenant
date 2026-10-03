using ArquitecturaBaseMultitenant.Domain.Features;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Escritura global y transaccional de módulos por espacio, con versión.</summary>
public interface ITenantFeatureRepository
{
    Task<TenantFeature?> GetForUpdateAsync(Guid tenantId, string key, uint expectedVersion, CancellationToken cancellationToken);
    void Add(TenantFeature feature);
    void MarkUpdated(TenantFeature feature);
}
