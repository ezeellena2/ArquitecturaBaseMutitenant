using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Contracts;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantContractTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task Business_configuration_is_accessible_with_configure_without_use_and_preserves_authentication_guards()
    {
        using var ownerClient = AssistantTestData.Client(factory);
        using var client = AssistantTestData.Client(factory);
        using var anonymous = await client.GetAsync("/api/organization/assistant/settings", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Configuration contract", Ct);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var memberId = await OrganizationJourney.AddActiveMemberAsync(factory, tenantId, account.UserId, Ct);
        await AssistantTestData.EnableAsync(factory, tenantId);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenantId);
        using var denied = await client.GetAsync("/api/organization/assistant/settings", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                var role = Role.Create("Configure only", null, RoleScope.Organization, null, [Permissions.Assistant.Configure]).Value;
                var db = services.GetRequiredService<ApplicationDbContext>();
                db.Add(role);
                db.Add(RoleAssignment.Assign(memberId, role.Id, role.Scope, null, null).Value);
                return Task.FromResult(ArquitecturaBaseMultitenant.Domain.Results.Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }
        // Configuration has its own permission; chat use is not a prerequisite.
        using var settings = await client.GetAsync("/api/organization/assistant/settings", Ct);
        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);
        using var available = await settings.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(JsonValueKind.Array, available!.RootElement.GetProperty("platformKeyModels").ValueKind);
        Assert.Equal(JsonValueKind.Array, available.RootElement.GetProperty("ownKeyModels").ValueKind);
        using var chat = await client.GetAsync("/api/organization/assistant", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, chat.StatusCode);
    }
    [Fact]
    public async Task Consumer_without_roles_reaches_501_with_an_explicit_available_feature_and_private_protocol_keys()
    {
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IFeatureAvailabilityPolicy>();
            services.AddScoped<IFeatureAvailabilityPolicy, AvailablePolicy>();
        }));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var anonymous = await client.GetAsync("/api/me/assistant", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        await AccountJourney.RegisterAsync(host, client, Ct);
        using var state = await client.GetAsync("/api/me/assistant", Ct);
        await Stub(state);
        using var profile = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        Assert.Empty(profile!.RootElement.GetProperty("permissions").EnumerateArray());
        Assert.Contains(profile.RootElement.GetProperty("features").EnumerateArray(), value => value.GetString() == "assistant");
        foreach (var path in new[] { "/api/me/assistant/conversations", "/api/me/assistant/quick/account" })
        {
            using var missing = await client.PostAsJsonAsync(path, new { }, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { }) };
            request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString("D"));
            using var result = await client.SendAsync(request, Ct);
            await Stub(result);
        }
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/me/assistant/conversations/{Guid.CreateVersion7():D}/messages")
            { Content = JsonContent.Create(new { text = "Contenido privado de prueba", attachmentIds = Array.Empty<Guid>(), conversationVersion = 0 }) };
        message.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString("D"));
        using var response = await client.SendAsync(message, Ct);
        await Stub(response);
        Assert.DoesNotContain("Contenido privado de prueba", await response.Content.ReadAsStringAsync(Ct));
    }
    [Fact]
    public async Task Default_chat_is_hidden_and_platform_management_can_configure_without_a_key_with_individual_permissions()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        using var consumer = await client.GetAsync("/api/me/assistant", Ct);
        Assert.Equal(HttpStatusCode.NotFound, consumer.StatusCode);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, [PlatformPermissions.Assistant.Configure], Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        using var settings = await client.GetAsync("/api/platform/assistant/settings", Ct);
        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);
        using var configuration = await settings.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.False(configuration!.RootElement.GetProperty("hasKey").GetBoolean());
        Assert.Equal(JsonValueKind.Null, configuration.RootElement.GetProperty("maskedKeySuffix").ValueKind);
        using var chat = await client.GetAsync("/api/platform/assistant", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, chat.StatusCode);
        using var usage = await client.GetAsync("/api/platform/assistant/usage?month=2026-10&page=1&pageSize=20", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, usage.StatusCode);
        using var profile = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        var capabilities = profile!.RootElement.GetProperty("moduleCapabilities").EnumerateArray();
        var assistant = Assert.Single(capabilities, value => value.GetProperty("key").GetString() == "assistant");
        Assert.False(assistant.GetProperty("available").GetBoolean());
    }
    private static async Task Stub(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal("General.Contract.NotImplemented", problem!.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
    }
    private sealed class AvailablePolicy : IFeatureAvailabilityPolicy
    {
        public string Key => "assistant";
        public Task<bool?> IsAvailableAsync(Access access, CancellationToken cancellationToken) => Task.FromResult<bool?>(true);
    }
}
