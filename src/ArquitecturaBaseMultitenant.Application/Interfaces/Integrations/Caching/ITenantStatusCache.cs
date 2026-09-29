using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;

public interface ITenantStatusCache
{
    Task<TenantStatus?> GetStatusAsync(Guid tenantId, CancellationToken cancellationToken);

    ValueTask InvalidateAsync(Guid tenantId, CancellationToken cancellationToken);
}
