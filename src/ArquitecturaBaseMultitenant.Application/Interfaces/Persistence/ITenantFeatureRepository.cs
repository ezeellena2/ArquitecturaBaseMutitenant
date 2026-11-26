using ArquitecturaBaseMultitenant.Domain.Features;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Escritura global y transaccional de módulos por espacio, con versión.</summary>
public interface ITenantFeatureRepository
{
    /// <summary>Serializa las escrituras de un módulo de una organización, dentro de la transacción y antes de leer.</summary>
    Task LockAsync(Guid tenantId, string key, CancellationToken cancellationToken);
    Task<TenantFeature?> GetForUpdateAsync(Guid tenantId, string key, uint expectedVersion, CancellationToken cancellationToken);
    void Add(TenantFeature feature);
    void MarkUpdated(TenantFeature feature);
}
