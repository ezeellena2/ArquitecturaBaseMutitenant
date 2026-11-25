using ArquitecturaBaseMultitenant.Application.Models.Settings;
using ArquitecturaBaseMultitenant.Domain.Settings;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Proyecta la configuración global que gobierna el registro y los límites de la plataforma.</summary>
public interface IPlatformSettingsReader
{
    Task<PlatformSettingsRow?> FindAsync(CancellationToken cancellationToken);

    /// <summary>Sólo las cuotas guardadas, desde la caché compartida y sin leer los correos de contacto; null sin configuración.</summary>
    Task<PlatformRateLimits?> FindRateLimitsAsync(CancellationToken cancellationToken);

    /// <summary>Los plazos de «Privacidad y retención» (272), de la base en cada uso; sin configuración, los de defecto.</summary>
    Task<IdentityRetentionPeriods> FindRetentionPeriodsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// El nombre, el remitente y el logo de los correos y del bot (273), de la base en cada uso; lo que no se guardó, del
    /// respaldo de arranque (<see cref="Integrations.Messaging.IEmailBrandingDefaults"/>).
    /// </summary>
    Task<PlatformBrandingRow> FindBrandingAsync(CancellationToken cancellationToken);
}
