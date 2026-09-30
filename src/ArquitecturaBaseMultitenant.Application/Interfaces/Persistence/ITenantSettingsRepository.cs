using ArquitecturaBaseMultitenant.Domain.Settings;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Obtiene y agrega configuración de la organización activa dentro de la transacción del caso de uso.</summary>
public interface ITenantSettingsRepository
{
    Task<TenantSettings?> GetCurrentAsync(CancellationToken cancellationToken);

    void Add(TenantSettings settings);
}
