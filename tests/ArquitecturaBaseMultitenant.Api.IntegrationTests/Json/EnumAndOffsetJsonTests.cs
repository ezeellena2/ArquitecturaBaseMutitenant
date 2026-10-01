using System.Net;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Json;

/// <summary>
/// Comprueba que los enums HTTP usen nombres y rechacen números. Incluye las restricciones de
/// representación temporal del contrato.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class EnumAndOffsetJsonTests(ApiFactory factory)
{
    [Fact]
    public async Task Http_accepts_enum_names()
    {
        using var client = factory.CreateClient();
        using var content = Json("""{"status":"Ready"}""");
        using var response = await client.PostAsync("/test/enum", content, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Ready", document.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Http_rejects_numeric_enum_with_problem_details()
    {
        using var client = factory.CreateClient();
        using var content = Json("""{"status":1}""");
        using var response = await client.PostAsync("/test/enum", content, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Request.Invalid", document.RootElement.GetProperty("code").GetString());
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");
}
