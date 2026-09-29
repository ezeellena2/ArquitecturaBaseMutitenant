using ArquitecturaBaseMultitenant.Domain.Settings;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface IPlatformSettingsRepository
{
    Task<PlatformSettings?> GetAsync(CancellationToken cancellationToken);

    void Add(PlatformSettings settings);
}
