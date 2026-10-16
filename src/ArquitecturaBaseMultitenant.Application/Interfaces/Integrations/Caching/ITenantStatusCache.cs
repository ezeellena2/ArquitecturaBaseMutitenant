using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;

/// <summary>
/// El estado de una organización en la caché compartida (ADR 0052), para que la autorización revalide suspensiones sin
/// consultar siempre la base; un cambio confirmado avanza la generación de su etiqueta.
/// </summary>
public interface ITenantStatusCache
{
    Task<TenantStatus?> GetStatusAsync(Guid tenantId, CancellationToken cancellationToken);
}
