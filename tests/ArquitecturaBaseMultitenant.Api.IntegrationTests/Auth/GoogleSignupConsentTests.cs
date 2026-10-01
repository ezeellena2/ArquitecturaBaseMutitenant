using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

/// <summary>
/// Comprueba que el desafío Google del registro requiera POST con antiforgery. Evita iniciar el alta desde
/// un GET o una solicitud sin protección.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class GoogleSignupConsentTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Signup_get_cannot_start_google_challenge_even_with_terms_query()
    {
        var challenge = new ChallengeTracker();
        using var host = HostWith(challenge);
        using var client = Client(host);

        using var response = await client.GetAsync(
            "/api/auth/external/google?signup=true&acceptedTerms=true&returnTo=%2F", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(challenge.WasChallenged);
    }

    [Fact]
    public async Task Signup_post_without_antiforgery_cannot_start_google_challenge()
    {
        var challenge = new ChallengeTracker();
        using var host = HostWith(challenge);
        using var client = Client(host);

        using var response = await client.PostAsync("/api/auth/external/google", SignupForm(), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal("Validation.Failed", problem?.RootElement.GetProperty("code").GetString());
        Assert.False(challenge.WasChallenged);
    }

    [Fact]
    public async Task Signup_post_with_antiforgery_starts_google_challenge()
    {
        var challenge = new ChallengeTracker();
        using var host = HostWith(challenge);
        using var client = Client(host);

        using var tokenResponse = await client.GetAsync(
            "/api/auth/external/google/antiforgery", Ct);
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        using var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        var requestToken = tokenBody!.RootElement.GetProperty("requestToken").GetString();
        Assert.False(string.IsNullOrEmpty(requestToken));

        using var response = await client.PostAsync("/api/auth/external/google",
            SignupForm(requestToken), Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(challenge.WasChallenged);
    }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> HostWith(
        ChallengeTracker tracker) => factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Authentication:Google:ClientId", string.Empty);
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(tracker);
                services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, FakeGoogleHandler>(
                    GoogleDefaults.AuthenticationScheme, _ => { });
            });
        });

    private static HttpClient Client(WebApplicationFactory<Program> host) =>
        host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });

    private static FormUrlEncodedContent SignupForm(string? requestToken = null) =>
        new(new Dictionary<string, string>
        {
            ["signup"] = "true",
            ["acceptedTerms"] = "true",
            ["returnTo"] = "/",
            ["culture"] = "es-AR",
            ["timeZoneId"] = "America/Argentina/Buenos_Aires",
            ["__RequestVerificationToken"] = requestToken ?? string.Empty,
        });

    private sealed class ChallengeTracker
    {
        public bool WasChallenged { get; set; }
    }

    private sealed class FakeGoogleHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ChallengeTracker tracker) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.NoResult());

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            tracker.WasChallenged = true;
            Response.Redirect(properties.RedirectUri ?? "/api/auth/external/callback");
            return Task.CompletedTask;
        }
    }
}
