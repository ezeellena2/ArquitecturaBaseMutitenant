using ArquitecturaBaseMultitenant.Application.Models.Settings;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface IPlatformSettingsReader
{
    Task<PlatformSettingsRow?> FindAsync(CancellationToken cancellationToken);
}
