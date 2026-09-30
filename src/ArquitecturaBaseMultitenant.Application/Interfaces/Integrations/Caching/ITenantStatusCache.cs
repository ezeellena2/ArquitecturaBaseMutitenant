using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;

/// <summary>Consulta y descarta el estado en caché de una organización para que la autorización revalide suspensiones sin consultar siempre la base.</summary>
public interface ITenantStatusCache
{
    Task<TenantStatus?> GetStatusAsync(Guid tenantId, CancellationToken cancellationToken);

    ValueTask InvalidateAsync(Guid tenantId, CancellationToken cancellationToken);
}
