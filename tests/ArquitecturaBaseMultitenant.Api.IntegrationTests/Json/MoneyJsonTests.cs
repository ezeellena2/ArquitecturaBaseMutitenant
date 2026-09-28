using System.Net;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Json;

[Collection(ApiTestGroup.Name)]
public sealed class MoneyJsonTests(ApiFactory factory)
{
    [Theory]
    [InlineData("ARS", "ARS")]
    [InlineData("ars", "ARS")]
    [InlineData("ZZZ", "ZZZ")]
    public async Task Money_round_trips_as_amount_and_iso_currency(string inputCurrency, string expectedCurrency)
    {
        using var client = factory.CreateClient();
        using var content = Json($$"""{"amount":1234.5,"currency":"{{inputCurrency}}"}""");

        using var response = await client.PostAsync("/test/money", content, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var money = document.RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, money.EnumerateObject().Count());
        Assert.Equal(1234.5m, money.GetProperty("amount").GetDecimal());
        Assert.Equal(expectedCurrency, money.GetProperty("currency").GetString());
    }

    [Theory]
    [InlineData("""{"amount":10,"currency":"AR$"}""")]
    [InlineData("""{"amount":10,"currency":"AR"}""")]
    [InlineData("""{"amount":10,"currency":null}""")]
    [InlineData("""{"amount":10}""")]
    [InlineData("""{"currency":"ARS"}""")]
    [InlineData("""{"amount":"10","currency":"ARS"}""")]
    public async Task Malformed_money_is_a_400_problem(string json)
    {
        using var client = factory.CreateClient();
        using var content = Json(json);

        using var response = await client.PostAsync("/test/money", content, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Request.Invalid", document.RootElement.GetProperty("code").GetString());
    }

    private static StringContent Json(string value) => new(value, Encoding.UTF8, "application/json");
}
