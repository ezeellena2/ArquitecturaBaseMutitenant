using ArquitecturaBaseMultitenant.Domain.Settings;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface ITenantSettingsRepository
{
    Task<TenantSettings?> GetCurrentAsync(CancellationToken cancellationToken);

    void Add(TenantSettings settings);
}
