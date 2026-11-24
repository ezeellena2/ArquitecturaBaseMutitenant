using ArquitecturaBaseMultitenant.Application.Models.Retention;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>
/// ADR 0062: la retención de las invitaciones de la organización activa. La llama un trabajo de fondo con
/// <c>TenantJobRunner</c>, una organización por vez y un lote por vez, cada uno en su transacción.
/// </summary>
public interface IInvitationRetentionService
{
    /// <summary>Borra un lote de invitaciones cerradas vencidas de plazo, salvo la última, desde <paramref name="after"/>.</summary>
    Task<Result<RetentionBatch>> PurgeClosedAsync(RetentionCursor? after, CancellationToken cancellationToken);
}
