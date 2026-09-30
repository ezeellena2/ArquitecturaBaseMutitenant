using ArquitecturaBaseMultitenant.Application.Models.Settings;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Proyecta la configuración global que gobierna el registro y los límites de la plataforma.</summary>
public interface IPlatformSettingsReader
{
    Task<PlatformSettingsRow?> FindAsync(CancellationToken cancellationToken);
}
