using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Security;

/// <summary>
/// Lo que el navegador ve de la Api (E9): toda cookie es Secure, HttpOnly, SameSite Lax como mínimo y sin Domain; Google
/// vuelve por GET con la cookie de correlación Lax y la vuelta real completa el desafío; la cookie de antiforgery es
/// estricta; y el issuer nunca sale del Host del pedido. La convivencia de las sesiones pública y de Plataforma la fija
/// <c>PlatformSessionTests</c>.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class BrowserSecurityTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Every_cookie_the_api_writes_is_secure_http_only_lax_and_host_only()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/test/cookie", Ct);

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        AssertBrowserSafe(cookie, "lax");
    }

    [Fact]
    public async Task Antiforgery_cookie_is_host_prefixed_and_strict()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/auth/external/google/antiforgery", Ct);

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-MtSignupCsrf=", StringComparison.Ordinal));
        AssertBrowserSafe(cookie, "strict");
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Google_comes_back_by_get_with_a_lax_correlation_cookie_and_completes_the_challenge()
    {
        var subject = Guid.NewGuid().ToString("N");
        await using var host = GoogleHost(subject);
        using var browser = host.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        using var challenge = await browser.GetAsync(
            "/api/auth/external/google?access=consumer&returnUrl=%2Fconnect%2Fauthorize%3Fclient_id%3Dweb", Ct);

        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        var google = challenge.Headers.Location!;
        Assert.Equal("accounts.google.com", google.Host);
        var parameters = QueryHelpers.ParseQuery(google.Query);
        // Sin response_mode, Google vuelve con un GET de navegación: la cookie Lax viaja.
        Assert.False(parameters.ContainsKey("response_mode"));
        Assert.Equal("code", parameters["response_type"].ToString());
        var correlation = Assert.Single(challenge.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(".AspNetCore.Correlation.", StringComparison.Ordinal));
        AssertBrowserSafe(correlation, "lax");

        using var callback = await browser.GetAsync("/signin-google?code=integration-code&state="
            + Uri.EscapeDataString(parameters["state"].ToString()), Ct);

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/api/auth/external/callback", callback.Headers.Location?.OriginalString);
        var external = Assert.Single(callback.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("Identity.External=", StringComparison.Ordinal));
        AssertBrowserSafe(external, "lax");

        // Sin la cookie de correlación, la vuelta no entra.
        using var stranger = host.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var forged = await stranger.GetAsync("/signin-google?code=integration-code&state="
            + Uri.EscapeDataString(parameters["state"].ToString()), Ct);
        Assert.Contains("error=Auth.ExternalLogin.Failed", forged.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_issuer_never_comes_from_the_request_host()
    {
        await using var api = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Authentication:Issuer", "https://example.test:8443/"));
        using var client = api.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://empresa.example.test:8443"), AllowAutoRedirect = false });

        using var discovery = await client.GetAsync("/.well-known/openid-configuration", Ct);

        Assert.Equal(HttpStatusCode.OK, discovery.StatusCode);
        using var document = await discovery.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        // Sólo el issuer es fijo: los endpoints de /connect se publican en el origen del pedido (multitenancy.md §5).
        Assert.Equal("https://example.test:8443/", document!.RootElement.GetProperty("issuer").GetString());
    }

    private static void AssertBrowserSafe(string cookie, string sameSite)
    {
        Assert.Contains("; secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; samesite=" + sameSite, cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    private WebApplicationFactory<Program> GoogleHost(string subject) => factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("Authentication:Google:ClientId", "integration-client");
        builder.UseSetting("Authentication:Google:ClientSecret", "integration-client-secret");
        builder.ConfigureTestServices(services => services.PostConfigure<GoogleOptions>(GoogleDefaults.AuthenticationScheme,
            options => options.Backchannel = new HttpClient(new GoogleBackchannel(subject))));
    });

    /// <summary>El token y los datos de la cuenta que Google devolvería del lado del servidor.</summary>
    private sealed class GoogleBackchannel(string subject) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            object body = request.Method == HttpMethod.Post
                ? new { access_token = "integration-access", token_type = "Bearer", expires_in = 3600 }
                : new
                {
                    sub = subject,
                    email = "google-" + subject + "@example.test",
                    email_verified = true,
                    given_name = "Ana",
                    family_name = "Prueba",
                    name = "Ana Prueba",
                };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body) });
        }
    }
}
