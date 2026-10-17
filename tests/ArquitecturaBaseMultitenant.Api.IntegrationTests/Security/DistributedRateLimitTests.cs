using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Security;

/// <summary>
/// Las cuotas compartidas de E9 sobre una base propia. Dos APIs cuentan en las mismas ventanas; cada IP, persona y
/// organización tiene su cuenta y el sitio público usa la suya. El rechazo es 429 con retryAfter y Retry-After. Plataforma
/// edita las doce cuotas con versión, y sin Redis el consumo sigue en PostgreSQL, sin reiniciarse al caer ni al volver.
/// Cada caso cuenta en un ambiente de Redis propio, así nada de la corrida se mezcla con sus contadores.
/// </summary>
public sealed class DistributedRateLimitTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string LoginCodeRoute = "/test/login-code-rate-limit";
    private const string ClientIpHeader = "X-Test-Client-IP";
    private const string DeadRedis = "127.0.0.1:1";

    // Las réplicas comparten la clave del HMAC de las IP; cada host de prueba sortearía la suya.
    private static readonly string HashKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Two_apis_share_each_window_and_a_rejection_tells_when_to_retry()
    {
        await SetLimitsAsync(TestRateLimits.Unlimited with { LoginCode = new(2, 900) });
        var environment = NewEnvironment();
        await using var first = Host(environment);
        await using var second = Host(environment);
        using var firstClient = first.CreateClient();
        using var secondClient = second.CreateClient();

        using (var admitted = await firstClient.GetAsync(LoginCodeRoute, Ct))
            Assert.Equal(HttpStatusCode.NoContent, admitted.StatusCode);
        using (var admitted = await secondClient.GetAsync(LoginCodeRoute, Ct))
            Assert.Equal(HttpStatusCode.NoContent, admitted.StatusCode);
        using var rejected = await firstClient.GetAsync(LoginCodeRoute, Ct);

        await AssertExceededAsync(rejected, 900);
    }

    [Theory]
    [InlineData("/api/auth/login-code", false)]
    [InlineData("/api/account-recovery/preview", true)]
    public async Task Each_ip_counts_apart_on_the_real_login_routes(string path, bool preview)
    {
        await SetLimitsAsync(TestRateLimits.Unlimited with { LoginCode = new(1, 900) });
        await using var host = Host(NewEnvironment());
        using var client = host.CreateClient();
        var (ip, otherIp) = (RandomIp(), RandomIp());

        using var first = await PostCodeAsync(client, path, ip, preview);
        using var other = await PostCodeAsync(client, path, otherIp, preview);
        using var again = await PostCodeAsync(client, path, ip, preview);

        // El primer pedido de cada IP llega a la ruta, que contesta su error de campo; el segundo de la misma IP, no.
        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, other.StatusCode);
        await AssertExceededAsync(again, 900);
    }

    [Fact]
    public async Task Each_person_and_each_organization_count_apart_with_their_signed_claims()
    {
        await using var host = Host(NewEnvironment());
        using var ownerClient = Browser(host);
        using var memberClient = Browser(host);
        var owner = await AccountJourney.RegisterAsync(host, ownerClient, Ct);
        var member = await AccountJourney.RegisterAsync(host, memberClient, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Cuotas " + Guid.NewGuid().ToString("N")[..8], Ct);
        await OrganizationJourney.AddActiveMemberAsync(factory, tenantId, member.UserId, Ct);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct, tenantId);
        await AccountJourney.AuthorizeAsync(memberClient, "business", Ct, tenantId);
        // Otra duración de ventana es otra cuenta: lo que gastó la preparación no cuenta acá.
        await SetLimitsAsync(TestRateLimits.Unlimited with { Identity = new(2, 86_400), Tenant = new(3, 86_400) });

        await AssertAdmittedAsync(ownerClient, "/api/me");
        await AssertAdmittedAsync(ownerClient, "/api/me");
        using (var person = await ownerClient.GetAsync("/api/me", Ct))
            await AssertExceededAsync(person, 86_400);
        // El rechazo no gastó la organización: la otra persona todavía entra una vez y completa sus tres.
        await AssertAdmittedAsync(memberClient, "/api/me");
        using (var organization = await memberClient.GetAsync("/api/me", Ct))
            await AssertExceededAsync(organization, 86_400);
        // Una ruta pública con sesión suma su identidad, que ya está llena.
        using var publicSite = await ownerClient.GetAsync("/api/public-site", Ct);
        await AssertExceededAsync(publicSite, 86_400);
    }

    [Fact]
    public async Task The_public_site_counts_its_own_ip_quota_and_leaves_the_main_one_alone()
    {
        await SetLimitsAsync(TestRateLimits.Unlimited with { PublicIp = new(1, 86_400), Ip = new(1, 86_400) });
        await using var host = Host(NewEnvironment());
        using var client = host.CreateClient();

        using (var site = await client.GetAsync("/api/public-site", Ct))
            Assert.Equal(HttpStatusCode.NotFound, site.StatusCode);
        using (var limited = await client.GetAsync("/api/public-site", Ct))
            await AssertExceededAsync(limited, 86_400);
        await AssertAdmittedAsync(client, "/api/contact");
        using var main = await client.GetAsync("/api/contact", Ct);
        await AssertExceededAsync(main, 86_400);
    }

    [Fact]
    public async Task Platform_edits_the_twelve_limits_with_its_version_and_the_next_request_uses_them()
    {
        await using var host = Host(NewEnvironment());
        using var operatorClient = Browser(host);
        var account = await AccountJourney.RegisterAsync(host, operatorClient, Ct);
        await PlatformOperators.GrantAsync(host.Services, account.UserId, PlatformOperators.All, Ct);
        await AccountJourney.AuthorizeAsync(operatorClient, "platform", Ct);
        await SetLimitsAsync(TestRateLimits.Unlimited);
        var (version, limits) = await ReadSettingsAsync(operatorClient);
        limits["loginCode"] = new JsonObject { ["permitLimit"] = 1, ["windowSeconds"] = 900 };

        using (var saved = await PutSettingsAsync(operatorClient, version, limits.DeepClone(), "Una sola prueba de código"))
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        // Otra IP: el alta del operador ya gastó la cuota de código de la suya.
        using var anonymous = host.CreateClient();
        var ip = RandomIp();
        using (var admitted = await SendAsync(anonymous, LoginCodeRoute, ip))
            Assert.Equal(HttpStatusCode.NoContent, admitted.StatusCode);
        using (var limited = await SendAsync(anonymous, LoginCodeRoute, ip))
            await AssertExceededAsync(limited, 900);

        using (var stale = await PutSettingsAsync(operatorClient, version, TestLimitsJson(), "Cambio obsoleto"))
        {
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            Assert.Equal("General.ConcurrencyConflict", await CodeAsync(stale));
        }

        var (current, stored) = await ReadSettingsAsync(operatorClient);
        Assert.Equal(1, stored["loginCode"]!["permitLimit"]!.GetValue<int>());
        using (var kept = await PutSettingsAsync(operatorClient, current, null, "Sin tocar las cuotas"))
            Assert.Equal(HttpStatusCode.OK, kept.StatusCode);
        var (latest, unchanged) = await ReadSettingsAsync(operatorClient);
        Assert.True(JsonNode.DeepEquals(stored, unchanged));

        var incomplete = unchanged.DeepClone().AsObject();
        incomplete.Remove("publicIp");
        using var invalid = await PutSettingsAsync(operatorClient, latest, incomplete, "Faltan perfiles");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var problem = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync(Ct));
        Assert.Equal("Validation.Failed", problem.RootElement.GetProperty("code").GetString());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("rateLimits.publicIp", out _));
    }

    [Fact]
    public async Task Without_redis_two_apis_count_together_in_postgres()
    {
        await SetLimitsAsync(TestRateLimits.Unlimited with { LoginCode = new(2, 900) });
        await using var first = Host(NewEnvironment(), DeadRedis);
        await using var second = Host(NewEnvironment(), DeadRedis);
        using var firstClient = first.CreateClient();
        using var secondClient = second.CreateClient();
        var ip = RandomIp();

        await AssertAdmittedAsync(firstClient, LoginCodeRoute, ip);
        await AssertAdmittedAsync(secondClient, LoginCodeRoute, ip);
        using var rejected = await SendAsync(firstClient, LoginCodeRoute, ip);

        await AssertExceededAsync(rejected, 900);
    }

    [Fact]
    public async Task Consumption_continues_when_redis_falls_and_when_it_comes_back()
    {
        await using var redis = new RedisBuilder("redis:8.6").Build();
        await redis.StartAsync(Ct);
        await SetLimitsAsync(TestRateLimits.Unlimited with { LoginCode = new(4, 900) });
        await using var host = Host(NewEnvironment(), redis.GetConnectionString());
        using var client = host.CreateClient();
        var ip = RandomIp();

        var partition = host.Services.GetRequiredService<IRateLimitIpHasher>().Hash(ip);

        await AssertAdmittedAsync(client, LoginCodeRoute, ip);
        await redis.PauseAsync(Ct);
        // Sin Redis cuenta PostgreSQL, sembrado con lo que ya se había gastado.
        await AssertAdmittedAsync(client, LoginCodeRoute, ip);
        await AssertAdmittedAsync(client, LoginCodeRoute, ip);
        Assert.Equal(3, await DurableCountAsync(partition));
        await redis.UnpauseAsync(Ct);
        await WaitHealthyAsync(client);
        // Al volver, Redis sigue desde lo que contó PostgreSQL: la cuarta entra y la quinta no.
        await AssertAdmittedAsync(client, LoginCodeRoute, ip);
        using var rejected = await SendAsync(client, LoginCodeRoute, ip);

        await AssertExceededAsync(rejected, 900);
        await using var connection = await ConnectionMultiplexer.ConnectAsync(redis.GetConnectionString());
        var server = connection.GetServer(connection.GetEndPoints().Single());
        var key = Assert.Single(await server.KeysAsync(pattern: "*{rl}:loginCode:*").ToArrayAsync(Ct));
        Assert.EndsWith(partition, key.ToString(), StringComparison.Ordinal);
        Assert.Equal(4, (int)await connection.GetDatabase().StringGetAsync(key));
    }

    [Fact]
    public async Task Cleanup_removes_only_the_durable_counters_of_finished_windows()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var nowUtc = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var finished = "cleanup:60:0:" + Guid.NewGuid().ToString("N");
        var current = "cleanup:60:1:" + Guid.NewGuid().ToString("N");
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO platform."RateLimitCounters" ("Key", "Count", "ExpiresAtUtc")
            VALUES ({finished}, 3, {nowUtc.AddMinutes(-1)}), ({current}, 3, {nowUtc.AddMinutes(1)})
            """, Ct);

        Assert.True((await scope.ServiceProvider.GetRequiredService<IRateLimitService>().CleanupExpiredAsync(Ct)).IsSuccess);

        var left = await context.Database.SqlQuery<string>($"""
            SELECT "Key" AS "Value" FROM platform."RateLimitCounters" WHERE "Key" IN ({finished}, {current})
            """).ToListAsync(Ct);
        Assert.Equal([current], left);
    }

    private WebApplicationFactory<Program> Host(string environment, string? redis = null) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Cache:Environment", environment);
            builder.UseSetting("Authentication:LoginCode:HashKey", HashKey);
            if (redis is not null) builder.UseSetting("ConnectionStrings:cache", redis);
            builder.ConfigureTestServices(services => services.AddSingleton<IStartupFilter, ClientIpStartupFilter>());
        });

    private static HttpClient Browser(WebApplicationFactory<Program> host) =>
        host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static string NewEnvironment() => "rl-" + Guid.NewGuid().ToString("N")[..20];

    private static string RandomIp() =>
        string.Join('.', [10, .. RandomNumberGenerator.GetBytes(3).Select(part => (int)part)]);

    private Task SetLimitsAsync(PlatformRateLimits limits) => TestRateLimits.SetAsync(factory.Services, limits, Ct);

    private async Task<int> DurableCountAsync(string partition)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var key = "loginCode:%:" + partition;
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database
            .SqlQuery<int>($"""SELECT "Count" AS "Value" FROM platform."RateLimitCounters" WHERE "Key" LIKE {key}""")
            .SingleAsync(Ct);
    }

    private static async Task WaitHealthyAsync(HttpClient client)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            using var health = await client.GetAsync("/health", Ct);
            if (health.IsSuccessStatusCode) return;
            await Task.Delay(TimeSpan.FromMilliseconds(200), Ct);
        }

        Assert.Fail("Redis did not come back.");
    }

    private static JsonObject TestLimitsJson() =>
        JsonSerializer.SerializeToNode(TestRateLimits.Unlimited, Web)!
            .AsObject();

    private static async Task<(uint Version, JsonObject Limits)> ReadSettingsAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/platform/settings", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var settings = (await response.Content.ReadFromJsonAsync<JsonObject>(Ct))!;
        return (settings["version"]!.GetValue<uint>(), settings["rateLimits"]!.DeepClone().AsObject());
    }

    private static Task<HttpResponseMessage> PutSettingsAsync(HttpClient client, uint version, JsonNode? limits,
        string reason) =>
        client.PutAsJsonAsync("/api/platform/settings", new JsonObject
        {
            ["consumerSignup"] = "Open",
            ["businessSignup"] = "Open",
            ["maxOwnedOrganizations"] = 1,
            ["salesEmail"] = "ventas@example.com",
            ["supportEmail"] = "soporte@example.com",
            ["version"] = version,
            ["reason"] = reason,
            ["rateLimits"] = limits,
        }, Ct);

    private static async Task<HttpResponseMessage> PostCodeAsync(HttpClient client, string path, string ip, bool preview)
    {
        var email = $"rate-{Guid.NewGuid():N}@example.test";
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(preview ? (object)new { previousEmail = email } : new { email }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        request.Headers.Add(ClientIpHeader, ip);
        return await client.SendAsync(request, Ct);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string path, string ip)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(ClientIpHeader, ip);
        return await client.SendAsync(request, Ct);
    }

    private static async Task AssertAdmittedAsync(HttpClient client, string path, string? ip = null)
    {
        using var response = ip is null ? await client.GetAsync(path, Ct) : await SendAsync(client, path, ip);
        Assert.True(response.StatusCode != HttpStatusCode.TooManyRequests, path + " was rejected by a quota.");
        Assert.True(response.IsSuccessStatusCode, path + " answered " + (int)response.StatusCode);
    }

    private static async Task AssertExceededAsync(HttpResponseMessage response, int windowSeconds)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(RateLimitErrors.ExceededCode, body.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("traceId").GetString()));
        var seconds = body.RootElement.GetProperty("retryAfter").GetInt32();
        Assert.InRange(seconds, 1, windowSeconds);
        Assert.Equal(seconds.ToString(CultureInfo.InvariantCulture), Assert.Single(response.Headers.GetValues("Retry-After")));
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return body.RootElement.GetProperty("code").GetString();
    }

    /// <summary>La IP del cliente de cada pedido, como la dejaría un proxy confiable.</summary>
    private sealed class ClientIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, following) =>
            {
                if (context.Request.Headers.TryGetValue(ClientIpHeader, out var value)
                    && IPAddress.TryParse(value.ToString(), out var ip))
                    context.Connection.RemoteIpAddress = ip;
                return following(context);
            });
            next(app);
        };
    }
}
