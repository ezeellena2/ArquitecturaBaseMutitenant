using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Bloquea el estado global antes de su lectura y verifica la versión del operador.</summary>
public interface IPlatformTenantRepository
{
    Task<Tenant?> GetForUpdateAsync(Guid tenantId, uint version, CancellationToken ct);
    void MarkUpdated(Tenant tenant);
}
