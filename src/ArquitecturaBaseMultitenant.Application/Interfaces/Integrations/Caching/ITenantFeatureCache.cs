using ArquitecturaBaseMultitenant.Application.Models.Features;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;

/// <summary>Cachea las habilitaciones crudas de cada espacio y se invalida después del commit.</summary>
public interface ITenantFeatureCache
{
    Task<IReadOnlyList<TenantFeatureRow>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
    ValueTask InvalidateAsync(Guid tenantId, CancellationToken cancellationToken);
}
