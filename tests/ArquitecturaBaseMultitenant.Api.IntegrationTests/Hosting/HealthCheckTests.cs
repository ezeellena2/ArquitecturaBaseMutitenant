using System.Net;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Hosting;

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
}
