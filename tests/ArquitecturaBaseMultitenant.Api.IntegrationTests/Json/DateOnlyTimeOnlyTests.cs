using System.Net;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Json;

/// <summary>
/// Comprueba las formas ISO exactas de fechas civiles y horas. Rechaza entradas ambiguas o que no respeten
/// el formato público.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class DateOnlyTimeOnlyTests(ApiFactory factory)
{
    [Fact]
    public async Task Civil_date_and_time_use_exact_iso_shapes()
    {
        using var client = factory.CreateClient();
        using var content = new StringContent("""{"date":"2026-09-27","time":"14:30:00"}""",
            Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/test/civil-time", content, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("2026-09-27", document.RootElement.GetProperty("date").GetString());
        Assert.Equal("14:30:00", document.RootElement.GetProperty("time").GetString());
    }

    [Theory]
    [InlineData("09/27/2026", "14:30:00")]
    [InlineData("2026-09-27", "14:30")]
    public async Task Civil_values_reject_noncanonical_input(string date, string time)
    {
        using var client = factory.CreateClient();
        using var content = new StringContent(JsonSerializer.Serialize(new { date, time }), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/test/civil-time", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
