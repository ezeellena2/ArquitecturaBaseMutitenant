using System.Net;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.Json;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Json;

/// <summary>
/// Comprueba lectura de offsets y escritura de instantes UTC con Z. Protege el formato de fracciones y
/// evita aceptar instantes sin referencia temporal explícita.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class UtcDateTimeTests(ApiFactory factory)
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new UtcDateTimeConverter() } };

    [Fact]
    public void Writes_utc_with_z_and_fraction_only_when_present()
    {
        Assert.Equal("\"2026-09-18T17:32:00Z\"",
            JsonSerializer.Serialize(new DateTime(2026, 9, 18, 17, 32, 0, DateTimeKind.Utc), Options));
        Assert.Equal("\"2026-09-18T17:32:00.12Z\"",
            JsonSerializer.Serialize(new DateTime(2026, 9, 18, 17, 32, 0, 120, DateTimeKind.Utc), Options));
    }

    [Theory]
    [InlineData("2026-09-18T10:00:00-03:00")]
    [InlineData("2026-09-18T13:00:00Z")]
    [InlineData("2026-09-18T15:00:00+02:00")]
    public void Reads_explicit_offset_as_utc(string value)
    {
        var instant = JsonSerializer.Deserialize<DateTime>($"\"{value}\"", Options);

        Assert.Equal(new DateTime(2026, 9, 18, 13, 0, 0, DateTimeKind.Utc), instant);
        Assert.Equal(DateTimeKind.Utc, instant.Kind);
    }

    [Theory]
    [InlineData("2026-09-18T10:00:00")]
    [InlineData("2026-09-18")]
    [InlineData("09/18/2026 13:00:00Z")]
    [InlineData("hola")]
    [InlineData("")]
    public void Rejects_noncanonical_or_offset_free_instant(string value) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DateTime>($"\"{value}\"", Options));

    [Fact]
    public async Task Http_returns_offset_input_as_z_and_preserves_null()
    {
        using var client = factory.CreateClient();
        using var content = Json("""{"occurredAtUtc":"2026-09-18T10:00:00-03:00","expiresAtUtc":null}""");
        using var response = await client.PostAsync("/test/dates", content, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("2026-09-18T13:00:00Z", document.RootElement.GetProperty("occurredAtUtc").GetString());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("expiresAtUtc").ValueKind);
    }

    [Fact]
    public async Task Http_rejects_instant_without_offset_with_problem_details()
    {
        using var client = factory.CreateClient();
        using var content = Json("""{"occurredAtUtc":"2026-09-18T10:00:00"}""");
        using var response = await client.PostAsync("/test/dates", content, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Request.Invalid", document.RootElement.GetProperty("code").GetString());
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");
}
