using ArquitecturaBaseMultitenant.Application.Models.Settings;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>
/// Las cuotas compartidas (E9): admite un pedido antes de que llegue al negocio y retira los contadores durables de
/// ventanas terminadas.
/// </summary>
public interface IRateLimitService
{
    /// <summary>Éxito si todas sus cuotas tienen lugar; si no, <c>Security.RateLimit.Exceeded</c> con los segundos que faltan.</summary>
    Task<Result> AdmitAsync(RateLimitRequest request, CancellationToken cancellationToken);

    Task<Result> CleanupExpiredAsync(CancellationToken cancellationToken);
}
