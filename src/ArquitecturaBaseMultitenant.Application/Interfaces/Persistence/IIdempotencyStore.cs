namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Distingue una reserva nueva, una operación en curso, una clave usada para otro pedido y una respuesta lista para repetir.</summary>
public enum IdempotencyReservationState
{
    Reserved,
    InProgress,
    Reused,
    Replay,
}

/// <summary>Transporta el estado de la clave y, si existe, la respuesta previa para evitar repetir el efecto del POST.</summary>
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
