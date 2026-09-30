using ArquitecturaBaseMultitenant.Application.Models.Legal;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Proyección de alcances de eliminación, sin datos privados ni perfiles de ingreso.</summary>
public interface IAccountDeletionTenantReader
{
    Task<IReadOnlyList<AccountDeletionTenant>> ListAsync(Guid userId, CancellationToken ct);
}
