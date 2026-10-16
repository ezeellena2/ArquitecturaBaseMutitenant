using ArquitecturaBaseMultitenant.Application.Models.Settings;
using ArquitecturaBaseMultitenant.Domain.Settings;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Proyecta la configuración global que gobierna el registro y los límites de la plataforma.</summary>
public interface IPlatformSettingsReader
{
    Task<PlatformSettingsRow?> FindAsync(CancellationToken cancellationToken);

    /// <summary>Sólo las cuotas guardadas, desde la caché compartida y sin leer los correos de contacto; null sin configuración.</summary>
    Task<PlatformRateLimits?> FindRateLimitsAsync(CancellationToken cancellationToken);
}
