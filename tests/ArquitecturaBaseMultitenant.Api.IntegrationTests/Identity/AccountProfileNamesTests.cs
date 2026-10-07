using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.Common;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Identity;

/// <summary>El editor guarda partes opcionales y lee el nombre armado por la identidad.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class AccountProfileNamesTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("  Jose\u0301  Luis  ", "  de  la Cruz  ", "José Luis", "de la Cruz", "José Luis de la Cruz")]
    [InlineData("Lucía", null, "Lucía", null, "Lucía")]
    [InlineData(null, "Fernández", null, "Fernández", "Fernández")]
    [InlineData("  ", "\t", null, null, null)]
    public async Task Parts_are_independent_optional_normalized_and_persist_the_full_name(
        string? firstName, string? lastName, string? expectedFirst, string? expectedLast, string? expectedDisplay)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await AccountJourney.RegisterAsync(factory, client, Ct);
        using var before = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        var version = before!.RootElement.GetProperty("version").GetUInt32();
        using var saved = await client.PutAsJsonAsync("/api/me", new
            { firstName, lastName, culture = "es-AR", timeZoneId = "America/Argentina/Buenos_Aires", version }, Ct);
        var savedVersion = await SavedVersion.ReadAsync(saved, Ct);
        using var after = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        Assert.True(after!.RootElement.TryGetProperty("firstName", out var first), "GET /api/me must expose firstName.");
        Assert.True(after.RootElement.TryGetProperty("lastName", out var last), "GET /api/me must expose lastName.");
        Assert.Equal(expectedFirst, first.GetString());
        Assert.Equal(expectedLast, last.GetString());
        Assert.Equal(expectedDisplay, after.RootElement.GetProperty("displayName").GetString());
        Assert.Equal(savedVersion, after.RootElement.GetProperty("version").GetUInt32());
        using var stale = await client.PutAsJsonAsync("/api/me", new
            { firstName = "Otro", lastName = "Nombre", culture = "en-US", timeZoneId = "America/Argentina/Buenos_Aires", version }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var staleProblem = await stale.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal("General.ConcurrencyConflict", staleProblem!.RootElement.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("firstName")]
    [InlineData("lastName")]
    public async Task Oversized_part_is_rejected_on_its_field_without_writing(string field)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await AccountJourney.RegisterAsync(factory, client, Ct);
        using var before = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        var version = before!.RootElement.GetProperty("version").GetUInt32();
        var body = new Dictionary<string, object?>
        {
            ["firstName"] = "Ana", ["lastName"] = "María", ["culture"] = "es-AR",
            ["timeZoneId"] = "America/Argentina/Buenos_Aires", ["version"] = version,
            [field] = new string('N', (field == "firstName" ? TextLimits.FirstName : TextLimits.LastName) + 1),
        };
        using var response = await client.PutAsJsonAsync("/api/me", body, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.True(problem!.RootElement.GetProperty("errors").TryGetProperty(field, out _));
        using var after = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        Assert.Equal(version, after!.RootElement.GetProperty("version").GetUInt32());
    }

    [Fact]
    public async Task Both_parts_can_reach_their_limit_without_truncating_the_full_name()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await AccountJourney.RegisterAsync(factory, client, Ct);
        using var before = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        var firstName = new string('李', TextLimits.FirstName);
        var lastName = new string('Ñ', TextLimits.LastName);
        using var response = await client.PutAsJsonAsync("/api/me", new
            { firstName, lastName, culture = "es-AR", timeZoneId = "America/Argentina/Buenos_Aires", version = before!.RootElement.GetProperty("version").GetUInt32() }, Ct);
        await SavedVersion.ReadAsync(response, Ct);
        using var after = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        Assert.Equal(firstName + " " + lastName, after!.RootElement.GetProperty("displayName").GetString());
    }
}
