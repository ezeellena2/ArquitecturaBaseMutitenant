using ArquitecturaBaseMultitenant.Application.Models.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Proyecta la configuración de la organización activa bajo el tenant ya resuelto.</summary>
public interface ITenantSettingsReader
{
    Task<TenantSettingsRow?> FindCurrentAsync(CancellationToken cancellationToken);
}
