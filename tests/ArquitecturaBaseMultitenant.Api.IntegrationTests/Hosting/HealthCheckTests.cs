using System.Net;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Hosting;

/// <summary>
/// Comprueba que la ruta de liveness responda correctamente y que la caída de Redis no saque la copia del tráfico.
/// Protege las señales que usa el entorno para saber si la API está activa y si puede recibir pedidos.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class HealthCheckTests(ApiFactory factory)
{
    [Fact]
    public async Task Liveness_endpoint_responds_ok()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/alive", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// ADR 0058: sin Redis la copia sigue en el tráfico, así que el chequeo de la caché falla en Degraded, que responde
    /// 200, también cuando lanza una excepción que no atrapa. No va en liveness.
    /// </summary>
    [Fact]
    public void A_redis_failure_degrades_readiness_and_stays_out_of_liveness()
    {
        var cache = factory.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
            .Single(registration => registration.Name == "cache");

        Assert.Equal(HealthStatus.Degraded, cache.FailureStatus);
        Assert.DoesNotContain("live", cache.Tags);
    }
}
