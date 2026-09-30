using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeKit;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed class IngressJourneyTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Signup_code_from_pickup_enters_a_personal_space_with_legal_acceptance()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var logs = new CapturedLogs();
        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        var email = $"journey-{Guid.NewGuid():N}@example.test";

        using var requested = await PostOnceAsync(client, "/api/auth/signup",
            new { email, acceptedTerms = true, culture = "es-AR" });
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);

        var code = await ReadPickupCodeAsync(host.Services, email);
        using var verified = await PostOnceAsync(client, "/api/auth/signup/verify",
            new { email, code, acceptedTerms = true, culture = "es-AR" });
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        Assert.Contains(verified.Headers.GetValues("Set-Cookie"),
            value => value.Contains("Identity.Application", StringComparison.Ordinal));

        await AssertPersonalSignupAsync(email);
        Assert.True(!logs.Entries.Any(entry => entry.Contains(code, StringComparison.Ordinal)),
            "Un código de ingreso apareció en logs.");
        Assert.True(!logs.Entries.Any(entry => entry.Contains(email, StringComparison.OrdinalIgnoreCase)),
            "Un correo completo apareció en logs.");
    }

    [Fact]
    public async Task Ana_enters_business_refreshes_switches_to_personal_changes_culture_and_logs_out()
    {
        var email = $"ana-journey-{Guid.NewGuid():N}@example.test";
        await using var isolatedFactory = new ApiFactory();
        await isolatedFactory.InitializeAsync();
        using var host = isolatedFactory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Email:Delivery", "PickupDirectory");
            builder.UseSetting("Authentication:Google:ClientId", "");
            builder.UseSetting("Seed:Development:AnaEmail", email);
        });
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        const string verifier = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";
        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var businessAuthorize = AuthorizePath("business", challenge);

        using var requested = await PostOnceAsync(client, "/api/auth/login-code", new { email });
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await ReadPickupCodeAsync(host.Services, email);
        using var verified = await PostOnceAsync(client, "/api/auth/login-code/verify",
            new { email, code, returnUrl = businessAuthorize });
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        Assert.Contains(verified.Headers.GetValues("Set-Cookie"),
            cookie => cookie.Contains("Identity.Application", StringComparison.Ordinal));

        var business = await AuthorizeAndExchangeAsync(client, businessAuthorize, verifier);
        using var businessMe = await GetMeAsync(client, business.AccessToken);
        Assert.Equal("business", businessMe.RootElement.GetProperty("access").GetString());
        var organization = Assert.Single(businessMe.RootElement.GetProperty("organizations").EnumerateArray());
        Assert.Equal("Empresa A", organization.GetProperty("name").GetString());
        var businessTenantId = businessMe.RootElement.GetProperty("activeTenantId").GetGuid();
        AssertIdTokenAccess(business.IdToken, "business", businessTenantId);
        Assert.Equal(organization.GetProperty("id").GetGuid(), businessTenantId);
        using var businessProbe = await PostAsBearerAsync(client,
            "/test/access/business-signup", business.AccessToken);
        Assert.Equal(HttpStatusCode.OK, businessProbe.StatusCode);
        using var anonymousProbe = await client.PostAsync("/test/access/business-signup", null, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousProbe.StatusCode);

        var refreshed = await ExchangeTokenAsync(client, new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = business.RefreshToken,
            ["client_id"] = "web",
        });
        using var refreshedMe = await GetMeAsync(client, refreshed.AccessToken);
        Assert.Equal("business", refreshedMe.RootElement.GetProperty("access").GetString());
        Assert.Equal(businessTenantId, refreshedMe.RootElement.GetProperty("activeTenantId").GetGuid());

        var consumer = await AuthorizeAndExchangeAsync(client, AuthorizePath("consumer", challenge), verifier);
        using var consumerMe = await GetMeAsync(client, consumer.AccessToken);
        Assert.Equal("consumer", consumerMe.RootElement.GetProperty("access").GetString());
        AssertIdTokenAccess(consumer.IdToken, "consumer",
            consumerMe.RootElement.GetProperty("activeTenantId").GetGuid());
        Assert.NotEqual(businessTenantId, consumerMe.RootElement.GetProperty("activeTenantId").GetGuid());
        using var personalProbe = await PostAsBearerAsync(client,
            "/test/access/business-signup", consumer.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, personalProbe.StatusCode);
        using var problem = await personalProbe.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(AccessErrors.WrongCode, problem?.RootElement.GetProperty("code").GetString());
        using var missingBusinessSignup = await PostAsBearerAsync(client,
            "/api/auth/business-signup", consumer.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, missingBusinessSignup.StatusCode);

        using var update = new HttpRequestMessage(HttpMethod.Put, "/api/me")
        {
            Content = JsonContent.Create(new
            {
                displayName = "Ana",
                culture = "en-US",
                timeZoneId = "America/Argentina/Buenos_Aires",
            }),
        };
        update.Headers.Authorization = new AuthenticationHeaderValue("Bearer", consumer.AccessToken);
        using var updated = await client.SendAsync(update, Ct);
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        using var englishMe = await GetMeAsync(client, consumer.AccessToken);
        Assert.Equal("en-US", englishMe.RootElement.GetProperty("culture").GetString());

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var formatter = scope.ServiceProvider.GetRequiredService<DisplayFormatter>();
            var spanish = await formatter.CreateAsync("es-AR", "America/Argentina/Buenos_Aires", Ct);
            var english = await formatter.CreateAsync(
                englishMe.RootElement.GetProperty("culture").GetString()!,
                englishMe.RootElement.GetProperty("timeZoneId").GetString()!, Ct);
            var day = new DateTime(2026, 9, 28, 15, 0, 0, DateTimeKind.Utc);
            Assert.NotEqual(formatter.FormatDate(day, spanish), formatter.FormatDate(day, english));
            Assert.NotEqual(formatter.FormatDecimal(1234.5m, 2, spanish),
                formatter.FormatDecimal(1234.5m, 2, english));
        }

        using var logout = await client.PostAsync("/connect/logout", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["client_id"] = "web",
                ["id_token_hint"] = consumer.IdToken,
                ["post_logout_redirect_uri"] = "https://localhost:5174/",
            }), Ct);
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal("https://localhost:5174/", logout.Headers.Location?.ToString());
        Assert.Contains(logout.Headers.GetValues("Set-Cookie"), cookie =>
            cookie.StartsWith(".AspNetCore.Identity.Application=;", StringComparison.Ordinal));

        using var revoked = await client.PostAsync("/connect/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = consumer.RefreshToken,
                ["client_id"] = "web",
            }), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, revoked.StatusCode);
    }

    private static string AuthorizePath(string access, string challenge) =>
        "/connect/authorize?client_id=web&response_type=code" +
        "&redirect_uri=https%3A%2F%2Flocalhost%3A5174%2Fcallback" +
        "&scope=openid%20profile%20email%20offline_access%20api" +
        "&code_challenge=" + challenge + "&code_challenge_method=S256&access=" + access;

    private static async Task<JourneyTokens> AuthorizeAndExchangeAsync(HttpClient client,
        string authorizePath, string verifier)
    {
        using var authorized = await client.GetAsync(authorizePath, Ct);
        Assert.Equal(HttpStatusCode.Redirect, authorized.StatusCode);
        var location = authorized.Headers.Location;
        Assert.NotNull(location);
        var parameters = QueryHelpers.ParseQuery(location.Query);
        Assert.True(parameters.TryGetValue("code", out var authorizationCode),
            "La autorización no entregó un código OIDC.");
        return await ExchangeTokenAsync(client, new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = authorizationCode.ToString(),
            ["client_id"] = "web",
            ["redirect_uri"] = "https://localhost:5174/callback",
            ["code_verifier"] = verifier,
        });
    }

    private static async Task<JourneyTokens> ExchangeTokenAsync(HttpClient client,
        Dictionary<string, string> form)
    {
        using var response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(form), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.NotNull(body);
        return new JourneyTokens(
            body.RootElement.GetProperty("access_token").GetString()!,
            body.RootElement.GetProperty("refresh_token").GetString()!,
            body.RootElement.GetProperty("id_token").GetString()!);
    }

    private static async Task<JsonDocument> GetMeAsync(HttpClient client, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonDocument>(Ct))!;
    }

    private static async Task<HttpResponseMessage> PostAsBearerAsync(HttpClient client,
        string path, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request, Ct);
    }

    private sealed record JourneyTokens(string AccessToken, string RefreshToken, string IdToken);

    private static void AssertIdTokenAccess(string token, string access, Guid? tenantId)
    {
        var payload = token.Split('.')[1];
        using var claims = JsonDocument.Parse(WebEncoders.Base64UrlDecode(payload));
        Assert.Equal(access, claims.RootElement.GetProperty("access").GetString());
        if (tenantId is not null)
        {
            Assert.Equal(tenantId.Value.ToString("D"),
                claims.RootElement.GetProperty("tenant_id").GetString());
        }
    }

    private static async Task<HttpResponseMessage> PostOnceAsync(HttpClient client, string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        return await client.SendAsync(request, Ct);
    }

    private static async Task<string> ReadPickupCodeAsync(IServiceProvider provider, string email)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var directory = Path.Combine(services.GetRequiredService<IHostEnvironment>().ContentRootPath, ".emails");
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await services.GetRequiredService<IOutboxDispatchService>().DispatchOnceAsync(
                services.GetServices<IChannelSender>().ToArray(), Ct);
            foreach (var path in Directory.Exists(directory) ? Directory.GetFiles(directory, "*.eml") : [])
            {
                string code;
                await using (var stream = File.OpenRead(path))
                {
                    using var message = await MimeMessage.LoadAsync(stream, Ct);
                    if (!message.To.Mailboxes.Any(mailbox => mailbox.Address == email)) continue;
                    var match = Regex.Match(message.TextBody ?? string.Empty, @"(?<!\d)\d{6}(?!\d)");
                    Assert.True(match.Success, "El correo pickup no contiene un código de seis dígitos.");
                    code = match.Value;
                }
                File.Delete(path);
                return code;
            }
            await Task.Delay(100, Ct);
        }
        throw new Xunit.Sdk.XunitException("No se encontró el correo pickup de registro.");
    }

    private async Task AssertPersonalSignupAsync(string email)
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT count(DISTINCT a."TenantId"), count(DISTINCT l."Id")
            FROM identity."LoginMethods" m
            JOIN identity."UserTenantAccesses" a ON a."UserId" = m."UserId"
            JOIN platform."Tenants" t ON t."Id" = a."TenantId"
            JOIN identity."LegalAcceptances" l ON l."UserId" = m."UserId"
            WHERE m."Value" = @email AND m."VerifiedAtUtc" IS NOT NULL
              AND t."Kind" = 'Personal' AND t."Status" = 'Active'
            """;
        command.Parameters.AddWithValue("email", email);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        Assert.Equal(1, reader.GetInt64(0));
        Assert.Equal(2, reader.GetInt64(1));
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _entries = new();

        public IEnumerable<string> Entries => _entries;

        public ILogger CreateLogger(string categoryName) => new CaptureLogger(_entries);

        public void Dispose() { }

        private sealed class CaptureLogger(ConcurrentQueue<string> entries) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => EmptyScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter) =>
                entries.Enqueue(formatter(state, exception));
        }

        private sealed class EmptyScope : IDisposable
        {
            public static EmptyScope Instance { get; } = new();

            public void Dispose() { }
        }
    }
}
