namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Reclamo durable y purga de credenciales globales de una cuenta vencida.</summary>
public interface IAccountDeletionRepository
{
    Task<Guid?> ClaimAsync(Guid leaseId, DateTime nowUtc, DateTime leaseExpiresAtUtc, CancellationToken ct);
    Task<bool> HasValidLeaseAsync(Guid userId, Guid leaseId, DateTime nowUtc, CancellationToken ct);
    Task PurgeCredentialsAsync(Guid userId, CancellationToken ct);
}
