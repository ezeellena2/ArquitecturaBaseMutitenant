using ArquitecturaBaseMultitenant.Application.Models.Settings;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;

/// <summary>
/// El contador compartido de cuotas (Redis, ADR 0052): admite o rechaza un pedido en todas sus cuotas a la vez y sólo suma
/// si todas tienen lugar, así un pedido rechazado no gasta las demás. Devuelve null si no responde; el servicio cuenta
/// entonces en PostgreSQL.
/// </summary>
public interface IRateLimitCounter
{
    Task<RateLimitTally?> TryAdmitAsync(IReadOnlyList<RateLimitBucket> buckets, CancellationToken cancellationToken);

    /// <summary>
    /// Lo último que esta instancia vio contar a cada cuota, o cero: siembra los contadores de PostgreSQL cuando Redis cae,
    /// para que el consumo siga y no se reinicie.
    /// </summary>
    IReadOnlyList<int> LastSeen(IReadOnlyList<RateLimitBucket> buckets);
}
