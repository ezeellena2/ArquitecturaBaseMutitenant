using ArquitecturaBaseMultitenant.Application.Models.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface ITenantSettingsReader
{
    Task<TenantSettingsRow?> FindCurrentAsync(CancellationToken cancellationToken);
}
