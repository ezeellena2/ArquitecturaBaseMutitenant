namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public enum IdempotencyReservationState
{
    Reserved,
    InProgress,
    Reused,
    Replay,
}

public sealed record IdempotencyReservation(
    Guid Id,
    IdempotencyReservationState State,
    int? ResponseStatusCode = null,
    string? ResponseBody = null);

/// <summary>Reserva técnica independiente del UnitOfWork del caso de uso.</summary>
public interface IIdempotencyStore
{
    Task<IdempotencyReservation> ReserveAsync(
        Guid? tenantId,
        Guid userId,
        Guid key,
        string bodyHash,
        string route,
        CancellationToken cancellationToken);

    Task CompleteAsync(Guid id, int statusCode, string responseBody, CancellationToken cancellationToken);

    Task ReleaseAsync(Guid id, CancellationToken cancellationToken);

    Task<int> CleanupExpiredAsync(CancellationToken cancellationToken);
}
