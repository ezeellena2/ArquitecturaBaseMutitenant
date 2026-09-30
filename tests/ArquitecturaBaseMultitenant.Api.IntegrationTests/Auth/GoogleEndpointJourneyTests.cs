using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed class GoogleEndpointJourneyTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string Verifier = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";

    [Fact]
    public async Task Google_signup_challenge_callback_cookie_and_consumer_token_reach_personal_me()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var identity = new FakeGoogleIdentity(Guid.NewGuid().ToString("N"),
            $"google-endpoint-{Guid.NewGuid():N}@example.test");
        using var host = HostWith(identity);
        using var client = Client(host);

        using var challenged = await client.GetAsync(
            "/api/auth/external/google?signup=true&acceptedTerms=true&access=consumer" +
            "&returnTo=%2F&culture=es-AR&timeZoneId=America%2FArgentina%2FBuenos_Aires", Ct);
        Assert.Equal(HttpStatusCode.Redirect, challenged.StatusCode);
        Assert.Equal("/api/auth/external/callback", challenged.Headers.Location?.ToString());
        Assert.Contains(challenged.Headers.GetValues("Set-Cookie"),
            cookie => cookie.Contains("Identity.External", StringComparison.Ordinal));

        // La query del callback no puede cambiar la aceptación ni la puerta guardadas en la cookie externa.
        using var callback = await client.GetAsync(
            "/api/auth/external/callback?acceptedTerms=false&access=business", Ct);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.StartsWith("/registro?google=complete", callback.Headers.Location?.ToString(),
            StringComparison.Ordinal);
        Assert.Contains(callback.Headers.GetValues("Set-Cookie"),
            cookie => cookie.Contains("Identity.Application", StringComparison.Ordinal));

        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(Verifier)));
        var authorize = AuthorizePath("consumer", challenge);
        using var authorized = await client.GetAsync(authorize, Ct);
        Assert.Equal(HttpStatusCode.Redirect, authorized.StatusCode);
        var code = QueryHelpers.ParseQuery(authorized.Headers.Location!.Query)["code"].ToString();
        Assert.NotEmpty(code);
        using var exchanged = await client.PostAsync("/connect/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = "web",
                ["redirect_uri"] = "https://localhost:5174/auth/callback",
                ["code_verifier"] = Verifier,
            }), Ct);
        Assert.Equal(HttpStatusCode.OK, exchanged.StatusCode);
        using var tokenBody = await exchanged.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        var accessToken = tokenBody!.RootElement.GetProperty("access_token").GetString();
        Assert.NotNull(accessToken);

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var me = await client.SendAsync(meRequest, Ct);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var account = await me.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal("consumer", account!.RootElement.GetProperty("access").GetString());
        Assert.True(account.RootElement.GetProperty("hasPersonalSpace").GetBoolean());
        Assert.NotEqual(Guid.Empty, account.RootElement.GetProperty("activeTenantId").GetGuid());
    }

    [Fact]
    public async Task Google_business_door_error_returns_to_business_login()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var identity = new FakeGoogleIdentity(Guid.NewGuid().ToString("N"),
            $"google-business-{Guid.NewGuid():N}@example.test");
        using var host = HostWith(identity);
        using var client = Client(host);
        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(Verifier)));
        var returnUrl = AuthorizePath("business", challenge);

        using var challenged = await client.GetAsync(
            "/api/auth/external/google?access=business&returnUrl=" +
            Uri.EscapeDataString(returnUrl), Ct);
        Assert.Equal(HttpStatusCode.Redirect, challenged.StatusCode);
        Assert.Equal("/api/auth/external/callback", challenged.Headers.Location?.ToString());
        using var callback = await client.GetAsync(
            "/api/auth/external/callback?access=consumer&signup=true&acceptedTerms=true", Ct);

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/login/empresa?error=Auth.Google.AccountNotFound",
            callback.Headers.Location?.ToString());
        Assert.False(callback.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(cookie => cookie.Contains("Identity.Application", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Google_callback_without_email_verified_claim_cannot_register()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var identity = new FakeGoogleIdentity(Guid.NewGuid().ToString("N"),
            $"google-unverified-{Guid.NewGuid():N}@example.test", EmailVerified: null);
        using var host = HostWith(identity);
        using var client = Client(host);

        using var challenged = await client.GetAsync(
            "/api/auth/external/google?signup=true&acceptedTerms=true&access=consumer" +
            "&returnTo=%2F&culture=es-AR&timeZoneId=America%2FArgentina%2FBuenos_Aires", Ct);
        Assert.Equal(HttpStatusCode.Redirect, challenged.StatusCode);

        using var callback = await client.GetAsync("/api/auth/external/callback", Ct);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/login?error=Auth.ExternalLogin.EmailNotVerified",
            callback.Headers.Location?.ToString());
        Assert.False(callback.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(cookie => cookie.Contains("Identity.Application", StringComparison.Ordinal)));
    }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> HostWith(FakeGoogleIdentity identity) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Authentication:Google:ClientId", string.Empty);
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(identity);
                services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, FakeGoogleHandler>(
                    GoogleDefaults.AuthenticationScheme, _ => { });
            });
        });

    private static HttpClient Client(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host) =>
        host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });

    private static string AuthorizePath(string access, string challenge) =>
        "/connect/authorize?client_id=web&response_type=code" +
        "&redirect_uri=https%3A%2F%2Flocalhost%3A5174%2Fauth%2Fcallback" +
        "&scope=openid%20profile%20email%20api" +
        "&code_challenge=" + challenge + "&code_challenge_method=S256&access=" + access;

    private sealed record FakeGoogleIdentity(string Subject, string Email, bool? EmailVerified = true);

    private sealed class FakeGoogleHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        FakeGoogleIdentity identity) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.NoResult());

        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            List<Claim> claims =
            [
                new(ClaimTypes.NameIdentifier, identity.Subject),
                new(ClaimTypes.Email, identity.Email),
                new(ClaimTypes.Name, "Persona nueva"),
            ];
            if (identity.EmailVerified is { } verified)
                claims.Add(new Claim("email_verified", verified ? "true" : "false"));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims,
                GoogleDefaults.AuthenticationScheme));
            await Context.SignInAsync(IdentityConstants.ExternalScheme, principal, properties);
            Response.Redirect(properties.RedirectUri ?? "/api/auth/external/callback");
        }
    }
}
