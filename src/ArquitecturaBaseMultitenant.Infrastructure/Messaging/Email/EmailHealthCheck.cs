using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

/// <summary>
/// N5: «email» en la readiness. Con las credenciales rechazadas (un 535) queda Degraded y no Unhealthy, hasta el próximo envío
/// que sale: el resto de la app sigue sirviendo y la copia no sale del tráfico. Mientras tanto los correos esperan en la cola
/// y se reintentan. Va sin el tag "live": un reinicio no arregla una contraseña revocada.
/// </summary>
internal sealed class EmailHealthCheck(EmailHealth health) : IHealthCheck
{
    public const string Name = "email";

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(health.CredentialsRejected
            ? HealthCheckResult.Degraded("The mail server rejected the credentials. Load a new app password in "
                + "Email:Smtp:Password and restart the Api; meanwhile the emails wait in the queue.")
            : HealthCheckResult.Healthy());
}
