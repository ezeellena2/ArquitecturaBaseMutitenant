using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.RateLimiting;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// Las cuotas del Asistente (E9): una clave nueva cuenta en assistantNewKey antes de probarla con el proveedor, y un rechazo
/// nunca llega al proveedor ni reinicia la cuota. Conservar la clave no cuenta. Las rutas del módulo declaran sus perfiles,
/// y un pedido sin sesión no cuenta en una cuota de persona: lo rechaza el acceso.
/// </summary>
public sealed class AssistantRateLimitTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_new_key_over_its_quota_never_reaches_the_provider_and_keeping_the_key_is_free()
    {
        var provider = new CountingKeyValidation();
        await using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Cache:Environment", "al-" + Guid.NewGuid().ToString("N")[..20]);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IChatClientFactory>();
                services.AddScoped<IChatClientFactory>(_ => provider);
            });
        });
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        await PlatformOperators.GrantAsync(host.Services, account.UserId, PlatformOperators.All, Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        await TestRateLimits.SetAsync(factory.Services, TestRateLimits.Unlimited with { AssistantNewKey = new(1, 86_400) }, Ct);

        var version = await PutKeyAsync(client, await VersionAsync(client), "replace", "rate-limit-first-1111");
        Assert.Equal(1, provider.Validations);
        using (var limited = await client.PutAsJsonAsync("/api/platform/assistant/settings",
            Body(await SettingsAsync(client), version, "replace", "rate-limit-second-2222"), Ct))
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
            using var problem = JsonDocument.Parse(await limited.Content.ReadAsStringAsync(Ct));
            Assert.Equal(RateLimitErrors.ExceededCode, problem.RootElement.GetProperty("code").GetString());
            Assert.InRange(problem.RootElement.GetProperty("retryAfter").GetInt32(), 1, 86_400);
            Assert.True(limited.Headers.Contains("Retry-After"));
        }

        Assert.Equal(1, provider.Validations);
        await PutKeyAsync(client, version, "keep", null);
        Assert.Equal(1, provider.Validations);
    }

    [Theory]
    [InlineData("api/me/assistant/quick/{key}", RateLimitProfile.AssistantQuickQuery)]
    [InlineData("api/organization/assistant/quick/{key}", RateLimitProfile.AssistantQuickQuery)]
    [InlineData("api/platform/assistant/quick/{key}", RateLimitProfile.AssistantQuickQuery)]
    [InlineData("api/me/assistant/conversations", RateLimitProfile.AssistantConversation)]
    [InlineData("api/organization/assistant/conversations/{id:guid}/messages", RateLimitProfile.AssistantConversation)]
    [InlineData("api/platform/assistant/conversations", RateLimitProfile.AssistantConversation)]
    public void Assistant_routes_declare_their_profile(string pattern, RateLimitProfile profile)
    {
        var endpoint = Assert.Single(factory.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>()
            .Endpoints.OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>(), candidate => candidate.RoutePattern.RawText == pattern
                && candidate.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods.Contains("POST") == true);

        Assert.Equal(profile, endpoint.Metadata.GetMetadata<RateLimitAttribute>()?.Profile);
    }

    [Theory]
    [InlineData("/api/me/assistant/quick/my_signin")]
    [InlineData("/api/organization/assistant/conversations")]
    [InlineData("/api/platform/assistant/conversations")]
    public async Task A_request_without_a_session_reaches_the_access_check_without_a_person_quota(string path)
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        using var response = await AccountJourney.PostAsync(client, path, new { }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<uint> PutKeyAsync(HttpClient client, uint version, string keyAction, string? apiKey)
    {
        using var saved = await client.PutAsJsonAsync("/api/platform/assistant/settings",
            Body(await SettingsAsync(client), version, keyAction, apiKey), Ct);
        return await SavedVersion.ReadAsync(saved, Ct);
    }

    private static async Task<uint> VersionAsync(HttpClient client) =>
        (await SettingsAsync(client)).RootElement.GetProperty("version").GetUInt32();

    private static async Task<JsonDocument> SettingsAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/platform/assistant/settings", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
    }

    private static object Body(JsonDocument settings, uint version, string keyAction, string? apiKey) => new
    {
        defaultModelId = settings.RootElement.GetProperty("defaultModelId").GetGuid(),
        allowedModelIds = settings.RootElement.GetProperty("allowedModelIds").EnumerateArray().Select(value => value.GetGuid()).ToArray(),
        effort = "low",
        dailyMessageLimit = 50,
        monthlyOrganizationBudget = new { amount = 100m, currency = "USD" },
        personalEnabled = false,
        retentionDays = 30,
        keyAction,
        apiKey,
        version,
    };

    /// <summary>El proveedor: sólo cuenta las pruebas de clave; esta prueba nunca conversa.</summary>
    private sealed class CountingKeyValidation : IChatClientFactory
    {
        private int _validations;

        internal int Validations => Volatile.Read(ref _validations);

        public Task<Result<IChatClient>> CreateAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("This fixture does not run chat.");

        public Task<Result> ValidateKeyAsync(string key, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _validations);
            return Task.FromResult(Result.Success());
        }
    }
}
