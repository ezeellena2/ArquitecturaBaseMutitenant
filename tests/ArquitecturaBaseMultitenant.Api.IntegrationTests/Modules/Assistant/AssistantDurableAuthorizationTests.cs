using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Idempotency;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantDurableAuthorizationTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    [Fact]
    public async Task Operator_durable_create_is_global_and_usage_remains_501_without_a_provider()
    {
        var provider = new NoChat();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<IChatClientFactory>(provider))));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(host.Services, account.UserId, PlatformOperators.All, Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client, "/api/platform/assistant/notice");
        var key = Guid.NewGuid();
        using var created = await AssistantDurableConversationTests.PostAsync(client, "/api/platform/assistant/conversations", "{}", key);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var replay = await AssistantDurableConversationTests.PostAsync(client, "/api/platform/assistant/conversations", "{}", key);
        Assert.Equal(await created.Content.ReadAsStringAsync(Ct), await replay.Content.ReadAsStringAsync(Ct));
        using var usage = await AssistantDurableConversationTests.PostAsync(client, "/api/platform/assistant/quick/assistant_usage", "{}", Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotImplemented, usage.StatusCode);
        using var scope = host.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Single(await database.Set<Domain.Assistant.PlatformAssistantConversation>().Where(row => row.OwnerUserId == account.UserId).ToArrayAsync(Ct));
        Assert.Single(await database.Set<Domain.Assistant.PlatformAssistantDurableOperation>().Where(row => row.OwnerUserId == account.UserId).ToArrayAsync(Ct));
        Assert.Null(scope.ServiceProvider.GetRequiredService<Application.Interfaces.Integrations.Request.ITenantContext>().TenantId);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task Organizations_links_use_two_owned_UUIDs_exclude_foreign_and_revalidate_history_and_replay()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var one = await OrganizationJourney.StageAsync(factory, account, "Durable organization one", Ct);
        var two = await OrganizationJourney.StageAsync(factory, account, "Durable organization two", Ct);
        using var outsiderClient = AssistantTestData.Client(factory);
        var outsider = await AccountJourney.RegisterAsync(factory, outsiderClient, Ct);
        var foreign = await OrganizationJourney.StageAsync(factory, outsider, "Durable organization foreign", Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client);
        var key = Guid.NewGuid();
        using var result = await AssistantDurableConversationTests.PostAsync(client, "/api/me/assistant/quick/my_organizations", "{}", key);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var body = await result.Content.ReadAsStringAsync(Ct);
        using var data = JsonDocument.Parse(body);
        var root = data.RootElement;
        var id = root.GetProperty("conversation").GetProperty("id").GetGuid();
        var ids = root.GetProperty("result").GetProperty("data").GetProperty("organizations").EnumerateArray().Select(row => row.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(new[] { one, two }.Order(), ids.Order());
        Assert.DoesNotContain(foreign, ids);
        var history = await client.GetFromJsonAsync<AssistantConversationResponse>($"/api/me/assistant/conversations/{id}", Json, Ct);
        var links = history!.Messages.Items.Single(row => row.Role == AssistantMessageRole.Assistant).Links;
        Assert.Equal(new[] { one, two }.Order(), links.Select(row => row.Parameters.TenantId!.Value).Order());
        Assert.All(links, link => Assert.Equal(AssistantRouteKey.PersonalOrganizations, link.RouteKey));
        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(two);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                Assert.True((await services.GetRequiredService<ApplicationDbContext>().Members.SingleAsync(row => row.UserId == account.UserId, ct)).Deactivate().IsSuccess);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        using var replay = await AssistantDurableConversationTests.PostAsync(client, "/api/me/assistant/quick/my_organizations", "{}", key);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        using var replayProblem = JsonDocument.Parse(await replay.Content.ReadAsStringAsync(Ct));
        Assert.Equal("Assistant.QuickQuery.NotAvailable", replayProblem.RootElement.GetProperty("code").GetString());
        using var revokedHistory = await client.GetAsync($"/api/me/assistant/conversations/{id}", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, revokedHistory.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Selected_tool_permission_is_checked_before_reserving_and_revoked_before_replay(bool revokeMember)
    {
        using var ownerClient = AssistantTestData.Client(factory);
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Durable permissions", Ct);
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var member = await OrganizationJourney.AddActiveMemberAsync(factory, tenantId, account.UserId, Ct);
        await AssistantTestData.EnableAsync(factory, tenantId);
        var use = Role.Create("Durable use", null, RoleScope.Organization, null, [Permissions.Assistant.Use]).Value;
        var data = Role.Create("Durable users", null, RoleScope.Organization, null, [Permissions.Users.Read]).Value;
        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<ApplicationDbContext>().AddRange(use, data, RoleAssignment.Assign(member, use.Id, use.Scope, null, null).Value);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenantId);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client, "/api/organization/assistant/notice");
        var key = Guid.NewGuid();
        using var denied = await AssistantDurableConversationTests.PostAsync(client, "/api/organization/assistant/quick/active_users", "{}", key);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        using var deniedProblem = JsonDocument.Parse(await denied.Content.ReadAsStringAsync(Ct));
        Assert.Equal("Assistant.QuickQuery.NotAvailable", deniedProblem.RootElement.GetProperty("code").GetString());
        Assert.True(deniedProblem.RootElement.TryGetProperty("traceId", out _));
        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
            var db = services.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.Set<IdempotencyKey>().AnyAsync(row => row.UserId == account.UserId && row.Key == key, Ct));
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                db.Add(RoleAssignment.Assign(member, data.Id, data.Scope, null, null).Value);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }
        using var success = await AssistantDurableConversationTests.PostAsync(client, "/api/organization/assistant/quick/active_users", "{}", key);
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var db = services.GetRequiredService<ApplicationDbContext>();
                if (revokeMember) Assert.True((await db.Members.SingleAsync(row => row.Id == member, ct)).Deactivate().IsSuccess);
                else db.Remove(await db.RoleAssignments.SingleAsync(row => row.MemberId == member && row.RoleId == data.Id, ct));
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        using var replay = await AssistantDurableConversationTests.PostAsync(client, "/api/organization/assistant/quick/active_users", "{}", key);
        Assert.Contains(replay.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest });
    }

    [Theory]
    [InlineData("gate")]
    [InlineData("legal")]
    [InlineData("account")]
    public async Task Consumer_durable_replay_requires_live_gate_legal_and_account(string revoke)
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client);
        var key = Guid.NewGuid();
        using var created = await AssistantDurableConversationTests.PostAsync(client, "/api/me/assistant/conversations", "{}", key);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        if (revoke == "gate") await AssistantTestData.PersonalAsync(factory, false);
        else
        {
            using var scope = factory.Services.CreateScope();
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var db = services.GetRequiredService<ApplicationDbContext>();
                if (revoke == "account") Assert.True((await db.Users.SingleAsync(row => row.Id == account.UserId, ct)).Suspend().IsSuccess);
                else
                {
                    var version = await db.LegalDocuments.Where(row => row.Kind == LegalDocumentKind.Terms).MaxAsync(row => row.Version, ct);
                    var document = LegalDocument.Create(LegalDocumentKind.Terms, version + 1, services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
                    db.AddRange(document, LegalDocumentContent.Create(document.Id, "es-AR", "Revisión legal de prueba."), LegalDocumentContent.Create(document.Id, "en-US", "Test legal revision."));
                }
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        using var replay = await AssistantDurableConversationTests.PostAsync(client, "/api/me/assistant/conversations", "{}", key);
        Assert.Equal(revoke == "gate" ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden, replay.StatusCode);
        await AssistantTestData.PersonalAsync(factory, true);
    }

    internal sealed class NoChat : IChatClientFactory
    {
        internal int Calls { get; private set; }
        public Task<Result<IChatClient>> CreateAsync(CancellationToken ct) { Calls++; return Task.FromResult(Result.Failure<IChatClient>(ContractErrors.NotImplemented)); }
        public Task<Result> ValidateKeyAsync(string key, CancellationToken ct) { Calls++; return Task.FromResult(Result.Failure(ContractErrors.NotImplemented)); }
    }
}
