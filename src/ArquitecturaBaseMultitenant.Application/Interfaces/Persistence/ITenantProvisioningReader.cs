using ArquitecturaBaseMultitenant.Application.Models.Platform;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Identifica el Dueño y la primera empresa bajo el alcance privado autorizado, sin ignorar filtros.</summary>
public interface ITenantProvisioningReader
{
    Task<TenantProvisioningSnapshot> FindAsync(Guid? registeringUserId, CancellationToken cancellationToken);
}
