using ArquitecturaBaseMultitenant.Application.Models.Retention;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>
/// ADR 0062: lo que borra la retención de los datos globales del ingreso, de a lotes y dentro de la transacción del
/// llamador. Borra por condición, sin cargar las filas.
/// </summary>
public interface IIdentityRetentionRepository
{
    /// <summary>Hasta <paramref name="limit"/> códigos creados antes de <paramref name="createdBeforeUtc"/>, los más viejos primero.</summary>
    Task<int> DeleteLoginCodesAsync(DateTime createdBeforeUtc, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Mira hasta <paramref name="limit"/> ingresos anteriores a <paramref name="occurredBeforeUtc"/>, después de
    /// <paramref name="after"/>, y borra los que no son el último ingreso exitoso de su cuenta.
    /// </summary>
    Task<RetentionBatch> DeleteLoginAuditsAsync(DateTime occurredBeforeUtc, RetentionCursor? after, int limit,
        CancellationToken cancellationToken);

    /// <summary>Hasta <paramref name="limit"/> contadores de los topes de mensajes de horas anteriores a <paramref name="hourBeforeUtc"/>.</summary>
    Task<int> DeleteMessageQuotaBucketsAsync(DateTime hourBeforeUtc, int limit, CancellationToken cancellationToken);
}
