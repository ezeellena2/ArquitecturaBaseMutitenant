using Microsoft.EntityFrameworkCore;
using ArquitecturaBaseMultitenant.Infrastructure.Idempotency;
using ArquitecturaBaseMultitenant.Domain.Assistant;
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
    public async Task Usage_preserves_authentication_access_permissions_and_honest_501_without_a_provider_key()
    {
        using var ownerClient = AssistantTestData.Client(factory);
        using var memberClient = AssistantTestData.Client(factory);
        const string businessUsage = "/api/organization/assistant/usage?month=2026-10&groupBy=day&page=1&pageSize=20";
        const string platformUsage = "/api/platform/assistant/usage?month=2026-10&page=1&pageSize=20";
        Assert.Equal(HttpStatusCode.Unauthorized, (await ownerClient.GetAsync(businessUsage, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ownerClient.GetAsync(platformUsage, Ct)).StatusCode);
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Usage contract", Ct);
        await AssistantTestData.EnableAsync(factory, tenantId);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct, tenantId);
        using var business = await ownerClient.GetAsync(businessUsage, Ct);
        await Stub(business);
        Assert.Equal(HttpStatusCode.Forbidden, (await ownerClient.GetAsync(platformUsage, Ct)).StatusCode);
        var member = await AccountJourney.RegisterAsync(factory, memberClient, Ct);
        await OrganizationJourney.AddActiveMemberAsync(factory, tenantId, member.UserId, Ct);
        await AccountJourney.AuthorizeAsync(memberClient, "business", Ct, tenantId);
        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.GetAsync(businessUsage, Ct)).StatusCode);
        using var operatorClient = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, operatorClient, Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, [PlatformPermissions.Assistant.Read], Ct);
        await AccountJourney.AuthorizeAsync(operatorClient, "platform", Ct);
        using var platform = await operatorClient.GetAsync(platformUsage, Ct);
        await Stub(platform);
        Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.GetAsync(businessUsage, Ct)).StatusCode);
    }
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
    public async Task Consumer_without_roles_preserves_stubs_and_uses_durable_private_operations_after_notice()
    {
        await using var ownFactory = new ApiFactory();
        await ownFactory.InitializeAsync();
        using var host = ownFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IFeatureAvailabilityPolicy>();
            services.AddScoped<IFeatureAvailabilityPolicy, AvailablePolicy>();
        }));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var anonymous = await client.GetAsync("/api/me/assistant", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        using var state = await client.GetAsync("/api/me/assistant", Ct);
        await Stub(state);
        using var profile = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        Assert.Empty(profile!.RootElement.GetProperty("permissions").EnumerateArray());
        Assert.Contains(profile.RootElement.GetProperty("features").EnumerateArray(), value => value.GetString() == "assistant");
        string[] paths = ["/api/me/assistant/conversations", "/api/me/assistant/quick/my_signin"];
        foreach (var path in paths)
        {
            using var missing = await client.PostAsJsonAsync(path, new { }, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
            using var denied = await AssistantDurableConversationTests.PostAsync(client, path, "{}", Guid.NewGuid());
            Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
            using var problem = await denied.Content.ReadFromJsonAsync<JsonDocument>(Ct);
            Assert.Equal(AssistantErrors.NoticeAcceptanceRequiredCode, problem!.RootElement.GetProperty("code").GetString());
        }
        await AssistantExecutionActorTests.AcceptNoticeAsync(client);
        foreach (var path in paths)
        {
            var key = Guid.NewGuid();
            var quick = path.EndsWith("/my_signin", StringComparison.Ordinal);
            var expected = quick ? HttpStatusCode.OK : HttpStatusCode.Created;
            using var result = await AssistantDurableConversationTests.PostAsync(client, path, "{}", key);
            Assert.Equal(expected, result.StatusCode);
            var body = await result.Content.ReadAsStringAsync(Ct);
            using var document = JsonDocument.Parse(body);
            var conversation = quick ? document.RootElement.GetProperty("conversation") : document.RootElement;
            Assert.NotEqual(Guid.Empty, conversation.GetProperty("id").GetGuid());
            Assert.NotEqual(0u, conversation.GetProperty("version").GetUInt32());
            if (quick)
            {
                Assert.Equal(2, document.RootElement.GetProperty("messages").GetArrayLength());
                Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("result").GetProperty("text").GetString()));
                Assert.DoesNotContain(account.Email.Value, body, StringComparison.Ordinal);
            }
            else Assert.NotNull(result.Headers.Location);
            using var replay = await AssistantDurableConversationTests.PostAsync(client, path, "{}", key);
            Assert.Equal(expected, replay.StatusCode);
            Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotent-Replayed")));
            Assert.Equal(body, await replay.Content.ReadAsStringAsync(Ct));
            await using var scope = host.Services.CreateAsyncScope();
            var stored = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<IdempotencyKey>()
                .AsNoTracking().SingleAsync(row => row.UserId == account.UserId && row.Key == key, Ct);
            Assert.Null(stored.ResponseBody);
            Assert.Equal(quick ? "assistant.quick" : "assistant.conversation", stored.ReferenceKind);
            Assert.NotNull(stored.ReferenceId);
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
