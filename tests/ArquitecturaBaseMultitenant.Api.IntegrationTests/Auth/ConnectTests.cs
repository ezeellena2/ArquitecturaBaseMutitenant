using System.Net;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed class ConnectTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("consumer", "/login")]
    [InlineData("business", "/login/empresa")]
    public async Task Authorize_without_cookie_preserves_the_selected_access_and_original_request(
        string access, string loginPath)
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });

        using var response = await client.GetAsync(AuthorizePath(access), Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.StartsWith(loginPath + "?returnUrl=", location, StringComparison.Ordinal);
        var decoded = Uri.UnescapeDataString(location);
        Assert.Contains("/connect/authorize?", decoded, StringComparison.Ordinal);
        Assert.Contains("access=", decoded, StringComparison.Ordinal);
        Assert.Contains(access, decoded, StringComparison.Ordinal);
        Assert.Contains("client_id=web", decoded, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Silent_authorize_without_cookie_returns_login_required_without_opening_the_login_page()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });

        using var response = await client.GetAsync(AuthorizePath("business") + "&prompt=none", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("error=login_required", response.Headers.Location?.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("/login", response.Headers.Location?.ToString(), StringComparison.Ordinal);
    }

    private static string AuthorizePath(string access) =>
        "/connect/authorize?client_id=web&response_type=code" +
        "&redirect_uri=https%3A%2F%2Flocalhost%3A5174%2Fcallback" +
        "&scope=openid%20profile%20email%20api" +
        "&code_challenge=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "&code_challenge_method=S256&access=" + access;
}
