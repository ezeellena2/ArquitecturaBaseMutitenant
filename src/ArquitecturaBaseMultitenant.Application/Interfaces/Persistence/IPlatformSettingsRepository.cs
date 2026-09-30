using ArquitecturaBaseMultitenant.Domain.Settings;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Consulta y agrega la configuración global de plataforma para su seed o actualización transaccional.</summary>
public interface IPlatformSettingsRepository
{
    Task<PlatformSettings?> GetAsync(CancellationToken cancellationToken);

    void Add(PlatformSettings settings);
}
