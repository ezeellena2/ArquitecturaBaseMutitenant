using ArquitecturaBaseMultitenant.Application.Models.Retention;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>
/// ADR 0062: el borrado de las invitaciones cerradas de la organización activa, de a lotes y dentro de la transacción del
/// llamador. Borra por condición, sin cargarlas: no deja una entrada de auditoría por fila.
/// </summary>
public interface IInvitationRetentionRepository
{
    /// <summary>
    /// Mira hasta <paramref name="limit"/> invitaciones cerradas que se mandaron antes de <paramref name="closedBeforeUtc"/>,
    /// después de <paramref name="after"/> en el orden en que se mandaron, y borra las que se cerraron antes de esa fecha
    /// y tienen otra más nueva de la misma persona al mismo destino. Nunca una pendiente, ni una cuyo vencimiento todavía no
    /// quedó en la auditoría.
    /// </summary>
    Task<RetentionBatch> DeleteClosedAsync(DateTime closedBeforeUtc, RetentionCursor? after, int limit,
        CancellationToken cancellationToken);
}
