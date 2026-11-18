using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>
/// Readiness de la caché compartida (ADR 0052 y 0058): mientras Redis no responde, <c>/health</c> queda en Degraded, que
/// responde 200, y la copia sigue en el tráfico, porque las lecturas y las cuotas caen a PostgreSQL. El estado de falla
/// sale del registro (<see cref="CachingRegistration"/>), así una excepción que no se atrapa también degrada.
/// <c>/alive</c> no lo incluye.
/// </summary>
internal sealed class RedisHealthCheck(RedisConnection connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            await (await connection.GetAsync()).GetDatabase().PingAsync();
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "The shared cache is unavailable.");
        }
    }
}
