using ArquitecturaBaseMultitenant.Application.Models.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface ITenantReader
{
    Task<TenantRow?> FindByIdAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TenantRow>> ListActiveBusinessesAsync(CancellationToken cancellationToken);
}
