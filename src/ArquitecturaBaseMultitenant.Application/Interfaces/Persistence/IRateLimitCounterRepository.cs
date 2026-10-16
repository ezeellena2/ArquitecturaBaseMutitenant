using ArquitecturaBaseMultitenant.Application.Models.Settings;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>
/// Los contadores de cuotas en PostgreSQL (E9), el respaldo compartido mientras Redis no responde. Trabaja adentro de la
/// unidad de trabajo del servicio de cuotas, nunca de la del negocio.
/// </summary>
public interface IRateLimitCounterRepository
{
    /// <summary>
    /// Toma con lock el contador de cada cuota, creándolo si falta y nunca por debajo de <paramref name="floors"/>, y
    /// devuelve cuánto lleva cada una, en el orden de las cuotas.
    /// </summary>
    Task<IReadOnlyList<int>> LockAsync(IReadOnlyList<RateLimitBucket> buckets, IReadOnlyList<int> floors,
        CancellationToken cancellationToken);

    /// <summary>Suma uno a cada cuota, ya tomada con <see cref="LockAsync"/>.</summary>
    Task IncrementAsync(IReadOnlyList<RateLimitBucket> buckets, CancellationToken cancellationToken);

    /// <summary>Borra hasta <paramref name="batchSize"/> contadores de ventanas terminadas y devuelve cuántos.</summary>
    Task<int> DeleteExpiredAsync(DateTime nowUtc, int batchSize, CancellationToken cancellationToken);
}
