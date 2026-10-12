using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Lecturas con seguimiento y remociones de la baja dentro de la transacción y el alcance del llamador.</summary>
public interface IAccountDeletionCleanupRepository
{
    void RequireTransaction();
    Task<Member?> GetMemberAsync(Guid userId, CancellationToken ct);
    Task<Tenant> GetPersonalSpaceAsync(Guid tenantId, CancellationToken ct);
    Task RemovePersonalDataAsync(CancellationToken ct);
    Task RemoveRecoveryRequestsAsync(Guid userId, CancellationToken ct);
}
