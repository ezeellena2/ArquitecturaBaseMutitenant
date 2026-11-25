using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

/// <summary>
/// «ai-keyring» en la readiness (<c>/health</c>, 0064 §11): pregunta a <see cref="AiKeyRingReadiness"/>, el mismo veredicto que usa el
/// worker. No coincidir es Unhealthy y nombra el id de la clave, nunca su valor ni el control: la copia no recibe tráfico (tampoco
/// las entradas de WhatsApp) ni toma turnos. Si la base no responde, la próxima prueba reintenta. Va sin el tag «live», como
/// WhatsApp: reiniciar no arregla un anillo equivocado. No descifra datos ni llama al proveedor.
/// </summary>
internal sealed class AiKeyRingHealthCheck(AiKeyRingReadiness readiness) : IHealthCheck
{
    public const string Name = "ai-keyring";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Guid> mismatched;
        try { mismatched = await readiness.CheckAsync(cancellationToken); }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("The key ring could not be checked against the database yet ("
                + exception.GetType().Name + "); the next probe retries.");
        }
        return mismatched.Count == 0
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("The key ring of this copy does not match the control values in the database for the key "
                + AiKeyRingReadiness.Keys(mismatched) + ". Load the ring the other copies use and restart this copy (docs/operations/runbook.md).");
    }
}
