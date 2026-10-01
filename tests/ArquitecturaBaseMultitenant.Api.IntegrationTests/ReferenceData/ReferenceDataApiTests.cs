using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.Contracts.ReferenceData;
using ArquitecturaBaseMultitenant.Api.Controllers.ReferenceData;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Models.ReferenceData;
using Microsoft.AspNetCore.Authorization;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.ReferenceData;

/// <summary>
/// Comprueba las rutas de catálogos, búsqueda, traducciones y ETag. Exige mostrar también valores
/// históricos deshabilitados para interpretar datos guardados.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class ReferenceDataApiTests(ApiFactory factory)
{
    [Fact]
    public async Task Whole_catalog_contains_enabled_and_disabled_rows_translated_to_the_request_culture()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/reference-data");
        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US"));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var body = document.RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("en-US", body.GetProperty("culture").GetString());
        foreach (var catalog in new[] { "currencies", "countries", "timeZones", "cultures", "taxIdTypes" })
        {
            var entries = body.GetProperty(catalog).EnumerateArray().ToArray();
            Assert.NotEmpty(entries);
            Assert.All(entries, entry => Assert.True(entry.GetProperty("isEnabled").ValueKind is
                JsonValueKind.True or JsonValueKind.False));
        }

        var currency = Assert.Single(body.GetProperty("currencies").EnumerateArray(),
            item => item.GetProperty("code").GetString() == "ARS");
        Assert.Equal("ARS", currency.GetProperty("code").GetString());
        Assert.Equal("Argentine Peso", currency.GetProperty("name").GetString());
        Assert.Equal("ARS", currency.GetProperty("displaySymbol").GetString());
        Assert.Contains(body.GetProperty("currencies").EnumerateArray(),
            item => item.GetProperty("code").GetString() == "AED"
                && !item.GetProperty("isEnabled").GetBoolean()
                && item.GetProperty("name").GetString() == "United Arab Emirates Dirham");
        Assert.Contains(body.GetProperty("countries").EnumerateArray(),
            item => item.GetProperty("code").GetString() == "AD" && !item.GetProperty("isEnabled").GetBoolean());
        Assert.Contains(body.GetProperty("timeZones").EnumerateArray(),
            item => item.GetProperty("id").GetString() == "Europe/Andorra" && !item.GetProperty("isEnabled").GetBoolean());
        var english = Assert.Single(body.GetProperty("cultures").EnumerateArray(),
            item => item.GetProperty("code").GetString() == "en-US");
        Assert.Equal("AM", english.GetProperty("amDesignator").GetString());
        Assert.Equal("PM", english.GetProperty("pmDesignator").GetString());
    }

    [Theory]
    [InlineData("currencies", "ARS", "code")]
    [InlineData("countries", "AR", "code")]
    [InlineData("time-zones", "Buenos", "city")]
    [InlineData("cultures", "es-AR", "code")]
    [InlineData("tax-id-types", "AR-CUIT", "code")]
    public async Task Each_catalog_has_a_searchable_route(string catalog, string search, string key)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/reference-data/{catalog}?search={Uri.EscapeDataString(search)}",
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = document.RootElement.EnumerateArray().ToArray();
        Assert.NotEmpty(entries);
        Assert.All(entries, item => Assert.True(item.GetProperty("isEnabled").ValueKind is
            JsonValueKind.True or JsonValueKind.False));
        Assert.Contains(entries, item => item.GetProperty(key).GetString()!.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("currencies", "AED", "code")]
    [InlineData("countries", "AD", "code")]
    [InlineData("time-zones", "Europe/Andorra", "id")]
    public async Task Per_catalog_search_keeps_disabled_rows_readable(string catalog, string search, string key)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/reference-data/{catalog}?search={Uri.EscapeDataString(search)}",
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = Assert.Single(document.RootElement.EnumerateArray(),
            item => item.GetProperty(key).GetString() == search);
        Assert.False(row.GetProperty("isEnabled").GetBoolean());
    }

    [Fact]
    public async Task Etag_supports_browser_revalidation()
    {
        using var client = factory.CreateClient();
        using var first = await client.GetAsync("/api/reference-data", TestContext.Current.CancellationToken);
        var etag = first.Headers.ETag;

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.NotNull(etag);
        Assert.Contains("must-revalidate", first.Headers.CacheControl?.ToString(), StringComparison.Ordinal);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/reference-data");
        request.Headers.IfNoneMatch.Add(etag);
        using var cached = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotModified, cached.StatusCode);
        Assert.Equal(etag, cached.Headers.ETag);
    }

    [Fact]
    public async Task Unsupported_language_falls_through_to_the_next_supported_request_language()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/reference-data");
        request.Headers.AcceptLanguage.ParseAdd("fr-FR,en-US;q=0.8");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("en-US", document.RootElement.GetProperty("culture").GetString());
        Assert.Equal("Argentine Peso",
            document.RootElement.GetProperty("currencies")[0].GetProperty("name").GetString());
    }

    [Fact]
    public void Reference_data_controller_declares_anonymous_access() =>
        Assert.True(typeof(ReferenceDataController).IsDefined(typeof(AllowAnonymousAttribute), inherit: true));

    [Fact]
    public void Http_contract_keeps_all_countries_of_a_shared_iana_zone()
    {
        var model = new ReferenceDataResponse("es-AR", [], [],
            [new TimeZoneReferenceItem("Shared/Zone", ["AR", "UY"], "Ciudad", true, null)], [], []);

        var response = ReferenceDataHttpResponse.FromModel(model);

        Assert.Equal(["AR", "UY"], Assert.Single(response.TimeZones).CountryCodes);
    }

    [Fact]
    public void Culture_http_contract_exposes_catalog_day_periods()
    {
        var model = new CultureReferenceItem("en-US", "en", "US", "MM/dd/yyyy", "h:mm tt",
            "MM/dd/yyyy h:mm tt", "MMMM d, yyyy", "AM", "PM", ".", ",", "{symbol}{number}",
            "{number}%", "es-AR", false, "English", true, null);

        var response = CultureReferenceHttpResponse.FromModel(model);

        Assert.Equal("AM", response.AmDesignator);
        Assert.Equal("PM", response.PmDesignator);
    }
}
