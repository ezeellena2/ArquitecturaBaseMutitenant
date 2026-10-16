using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>
/// Readiness de la caché compartida (ADR 0052): <c>/health</c> falla mientras Redis no responde. <c>/alive</c> no lo
/// incluye, y las rutas de negocio siguen leyendo de PostgreSQL.
/// </summary>
internal sealed class RedisHealthCheck(RedisConnection connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await (await connection.GetAsync()).GetDatabase().PingAsync();
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            return HealthCheckResult.Unhealthy("The shared cache is unavailable.");
        }
    }
}
