using ArquitecturaBaseMultitenant.Application.Models.Features;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;

/// <summary>
/// Las habilitaciones crudas de cada espacio en la caché compartida (ADR 0052); un cambio confirmado avanza la generación
/// de su etiqueta.
/// </summary>
public interface ITenantFeatureCache
{
    Task<IReadOnlyList<TenantFeatureRow>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
}
