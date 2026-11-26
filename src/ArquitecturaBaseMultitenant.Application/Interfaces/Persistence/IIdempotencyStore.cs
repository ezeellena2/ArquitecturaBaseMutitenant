using ArquitecturaBaseMultitenant.Application.Models.Idempotency;

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
    string? ResponseBody = null,
    IdempotentReference? Reference = null,
    string? ResponseLocation = null);

/// <summary>
/// Lo que el almacén sabe de una clave vigente: si terminó (<c>ResponseStatusCode</c>; null mientras está en curso), la ruta
/// que la reservó y el enlace a lo creado. Lo usa la propuesta de una acción para saber si un intento ya se guardó (ADR 0068 §10).
/// </summary>
public sealed record IdempotencyKeyState(int? ResponseStatusCode, string Route, string? ResponseLocation);

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

    Task CompleteAsync(Guid id, int statusCode, string responseBody, string? responseLocation, CancellationToken cancellationToken);

    Task CompleteReferenceAsync(Guid id, IdempotentReference reference, CancellationToken cancellationToken) =>
        throw new NotSupportedException("This store does not support durable references.");

    Task ReleaseAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>La clave de esa persona en ese alcance, si sigue vigente; vencida, null, porque el almacén la puede volver a reservar.</summary>
    Task<IdempotencyKeyState?> FindAsync(Guid? tenantId, Guid userId, Guid key, CancellationToken cancellationToken) =>
        throw new NotSupportedException("This store does not read keys.");

    Task<int> CleanupExpiredAsync(CancellationToken cancellationToken);
}
