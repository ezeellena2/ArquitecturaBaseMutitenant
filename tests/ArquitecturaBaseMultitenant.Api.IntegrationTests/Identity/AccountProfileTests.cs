using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Identity;

[Collection(ApiTestGroup.Name)]
public sealed class AccountProfileTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Profile_requires_its_read_version_and_stale_save_preserves_first_preferences()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await AccountJourney.RegisterAsync(factory, client, Ct);
        using var read = await client.GetAsync("/api/me", Ct);
        using var before = await read.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        var version = before!.RootElement.GetProperty("version").GetUInt32();
        Assert.False(before.RootElement.GetProperty("needsPersonalLoginMethod").GetBoolean());
        using var missing = await client.PutAsJsonAsync("/api/me", new
            { displayName = "Persona", culture = "en-US", timeZoneId = "America/Argentina/Buenos_Aires" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using var first = await client.PutAsJsonAsync("/api/me", new
            { displayName = "Persona", culture = "en-US", timeZoneId = "America/Argentina/Buenos_Aires", version }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        using var stale = await client.PutAsJsonAsync("/api/me", new
            { displayName = "Otra persona", culture = "es-AR", timeZoneId = "America/Argentina/Buenos_Aires", version }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var problem = await stale.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal("General.ConcurrencyConflict", problem!.RootElement.GetProperty("code").GetString());
        using var loaded = await client.GetAsync("/api/me", Ct);
        using var after = await loaded.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal("en-US", after!.RootElement.GetProperty("culture").GetString());
        Assert.Equal("Persona", after.RootElement.GetProperty("displayName").GetString());
        Assert.NotEqual(version, after.RootElement.GetProperty("version").GetUInt32());
    }
}
