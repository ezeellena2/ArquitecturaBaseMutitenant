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
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
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

/// <summary>
/// Recorre desafío, callback, cookie y emisión de tokens del ingreso con Google. Comprueba el retorno
/// correcto y la prueba protegida de una cuenta con baja pendiente.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class GoogleEndpointJourneyTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string Verifier = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";

    [Fact]
    public async Task Pending_Google_callback_keeps_ticket_in_secure_cookie_and_expires_it()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var clock = new FakeTimeProvider(factory.Services.GetRequiredService<TimeProvider>().GetUtcNow());
        var identity = new FakeGoogleIdentity(Guid.NewGuid().ToString("N"), $"google-grace-{Guid.NewGuid():N}@example.test");
        using var host = HostWith(identity, clock);
        using var client = Client(host);
        using var challenged = await StartGoogleSignupAsync(client);
        using var registered = await client.GetAsync("/api/auth/external/callback", Ct);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var userId = await services.GetRequiredService<ApplicationDbContext>().LoginMethods
                .Where(row => row.Value == identity.Subject).Select(row => row.UserId).SingleAsync(Ct);
            var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(
                ct => services.GetRequiredService<IUserRepository>().RequestDeletionAsync(userId,
                    "Prueba Google", clock.GetUtcNow().UtcDateTime, 30, ct), CommitPolicy.OnSuccess, Ct);
            Assert.True(result.IsSuccess);
        }
        var returnUrl = AuthorizePath("business", WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(Verifier))));
        using var signIn = await client.GetAsync("/api/auth/external/google?access=business&returnUrl=" + Uri.EscapeDataString(returnUrl), Ct);
        using var callback = await client.GetAsync("/api/auth/external/callback", Ct);
        Assert.Equal("/login/empresa?error=Identity.Account.PendingDeletion", callback.Headers.Location?.ToString());
        var cookie = Assert.Single(callback.Headers.GetValues("Set-Cookie"), value => value.StartsWith("MtPendingDeletion=", StringComparison.Ordinal));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Identity.Application", cookie, StringComparison.Ordinal);
        using var pending = await client.PostAsJsonAsync("/api/auth/deletion/pending", new { }, Ct);
        Assert.Equal(HttpStatusCode.OK, pending.StatusCode);
        using var body = await pending.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(returnUrl, body!.RootElement.GetProperty("returnUrl").GetString());
        Assert.False(string.IsNullOrEmpty(body.RootElement.GetProperty("cancelTicket").GetString()));
        clock.Advance(TimeSpan.FromMinutes(5));
        using var expired = await client.PostAsJsonAsync("/api/auth/deletion/pending", new { }, Ct);
        Assert.Equal(HttpStatusCode.NotFound, expired.StatusCode);
    }

    [Fact]
    public async Task Google_signup_challenge_callback_cookie_and_consumer_token_reach_personal_me()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var identity = new FakeGoogleIdentity(Guid.NewGuid().ToString("N"),
            $"google-endpoint-{Guid.NewGuid():N}@example.test");
        using var host = HostWith(identity);
        using var client = Client(host);

        using var challenged = await StartGoogleSignupAsync(client);
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

        using var challenged = await StartGoogleSignupAsync(client);
        Assert.Equal(HttpStatusCode.Redirect, challenged.StatusCode);

        using var callback = await client.GetAsync("/api/auth/external/callback", Ct);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/login?error=Auth.ExternalLogin.EmailNotVerified",
            callback.Headers.Location?.ToString());
        Assert.False(callback.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(cookie => cookie.Contains("Identity.Application", StringComparison.Ordinal)));
    }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> HostWith(FakeGoogleIdentity identity, TimeProvider? clock = null) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Authentication:Google:ClientId", string.Empty);
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(identity);
                if (clock is not null)
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton(clock);
                }
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

    private static async Task<HttpResponseMessage> StartGoogleSignupAsync(HttpClient client)
    {
        using var tokenResponse = await client.GetAsync(
            "/api/auth/external/google/antiforgery", Ct);
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        using var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        var requestToken = tokenBody!.RootElement.GetProperty("requestToken").GetString();
        Assert.False(string.IsNullOrEmpty(requestToken));

        return await client.PostAsync("/api/auth/external/google", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["signup"] = "true",
                ["acceptedTerms"] = "true",
                ["access"] = "consumer",
                ["returnTo"] = "/",
                ["culture"] = "es-AR",
                ["timeZoneId"] = "America/Argentina/Buenos_Aires",
                ["__RequestVerificationToken"] = requestToken,
            }), Ct);
    }

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
