using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Models.Profile;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Services.Profile;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantHistoryLinkTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Personal_history_filters_each_message_with_one_live_profile_and_propagates_profile_errors(bool profileFails)
    {
        var spy = new ProfileProbe();
        var provider = new AssistantDurableAuthorizationTests.NoChat();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<IChatClientFactory>(provider));
            services.Replace(ServiceDescriptor.Scoped<IProfileService>(serviceProvider =>
                new ObservedProfile(ActivatorUtilities.CreateInstance<ProfileService>(serviceProvider), spy)));
        }));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        var one = await OrganizationJourney.StageAsync(factory, account, "History first", Ct);
        var two = await OrganizationJourney.StageAsync(factory, account, "History second", Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client);
        using var me = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        Assert.NotNull(me);
        var personal = me.RootElement.GetProperty("activeTenantId").GetGuid();
        var conversation = await AssistantTestData.ConversationAsync(factory, personal, account.UserId, Access.Consumer);
        using (var baselineScope = factory.Services.CreateScope())
        {
            var services = baselineScope.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(personal);
            conversation = await services.GetRequiredService<ApplicationDbContext>().Set<AssistantConversation>().AsNoTracking()
                .SingleAsync(row => row.Id == conversation.Id, Ct);
        }
        var firstLinks = new[]
        {
            Link(AssistantRouteKey.PersonalOrganizations, tenant: one),
            Link(AssistantRouteKey.PersonalOrganizations, tenant: Guid.NewGuid()),
            Link(AssistantRouteKey.PersonalAccount),
            Link(AssistantRouteKey.BusinessHome),
            Link(AssistantRouteKey.PersonalAccount, tenant: one),
            Link(AssistantRouteKey.PersonalAccount, company: Guid.NewGuid()),
            Link((AssistantRouteKey)999),
        };
        var secondLinks = new[]
        {
            Link(AssistantRouteKey.PersonalOrganizations, tenant: two),
            Link(AssistantRouteKey.PersonalOrganizations, tenant: one),
            Link(AssistantRouteKey.PersonalOrganizations),
        };
        var first = await AddMessageAsync(personal, conversation.Id, firstLinks);
        var second = await AddMessageAsync(personal, conversation.Id, secondLinks);
        spy.Calls = 0;
        spy.Fail = profileFails;
        using var response = await client.GetAsync($"/api/me/assistant/conversations/{conversation.Id}", Ct);
        Assert.Equal(1, spy.Calls);
        Assert.Equal(0, provider.Calls);
        if (profileFails)
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
            Assert.Equal(UserErrors.NotFoundCode, problem.RootElement.GetProperty("code").GetString());
            return;
        }
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var history = await response.Content.ReadFromJsonAsync<AssistantConversationResponse>(Json, Ct);
        Assert.NotNull(history);
        Assert.Equal(conversation.Version, history.Conversation.Version);
        Assert.Equal(conversation.LastActivityAtUtc, history.Conversation.LastActivityAtUtc);
        Assert.Equal(JsonSerializer.Serialize(new[] { firstLinks[0], firstLinks[2] }, Json),
            JsonSerializer.Serialize(history.Messages.Items.Single(row => row.Id == first.Id).Links, Json));
        Assert.Equal(JsonSerializer.Serialize(secondLinks[..2], Json),
            JsonSerializer.Serialize(history.Messages.Items.Single(row => row.Id == second.Id).Links, Json));
        await AssertStoredMessageAsync(personal, first);
        await AssertStoredMessageAsync(personal, second);
    }

    [Fact]
    public async Task Business_history_keeps_only_active_companies_of_the_current_organization()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, account, "History company scope", Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenantId);
        var active = await OrganizationJourney.CreateCompanyAsync(client, "History active", Ct);
        var inactive = await OrganizationJourney.CreateCompanyAsync(client, "History inactive", Ct);
        var otherTenant = await OrganizationJourney.StageAsync(factory, account, "History other scope", Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, otherTenant);
        var foreign = await OrganizationJourney.CreateCompanyAsync(client, "History foreign", Ct);
        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                (await services.GetRequiredService<ApplicationDbContext>().Companies.SingleAsync(row => row.Id == inactive, ct))
                    .SetStatus(CompanyStatus.Inactive);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        await AssistantTestData.EnableAsync(factory, tenantId);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenantId);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client, "/api/organization/assistant/notice");
        var conversation = await AssistantTestData.ConversationAsync(factory, tenantId, account.UserId, Access.Business);
        var links = new[]
        {
            Link(AssistantRouteKey.BusinessCompanies, company: active),
            Link(AssistantRouteKey.BusinessCompanies, company: inactive),
            Link(AssistantRouteKey.BusinessCompanies, company: foreign),
            Link(AssistantRouteKey.BusinessCompanies),
        };
        var message = await AddMessageAsync(tenantId, conversation.Id, links);
        using var response = await client.GetAsync($"/api/organization/assistant/conversations/{conversation.Id}", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var history = await response.Content.ReadFromJsonAsync<AssistantConversationResponse>(Json, Ct);
        Assert.NotNull(history);
        Assert.Equal(JsonSerializer.Serialize(new[] { links[0], links[3] }, Json),
            JsonSerializer.Serialize(Assert.Single(history.Messages.Items).Links, Json));
        await AssertStoredMessageAsync(tenantId, message);
    }

    private async Task<AssistantMessage> AddMessageAsync(Guid tenantId, Guid conversationId, IReadOnlyList<AssistantLinkResponse> links)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var message = AssistantMessage.Create(conversationId, "Assistant", "Complete", "Original history text",
            services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
        message.SetLinks(JsonSerializer.Serialize(links));
        message.SetFeedback(true);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ApplicationDbContext>().Add(message);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        // Compare against PostgreSQL's stored jsonb representation before the GET.
        return await services.GetRequiredService<ApplicationDbContext>().Set<AssistantMessage>().AsNoTracking()
            .SingleAsync(row => row.Id == message.Id, Ct);
    }

    private async Task AssertStoredMessageAsync(Guid tenantId, AssistantMessage original)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var stored = await services.GetRequiredService<ApplicationDbContext>().Set<AssistantMessage>().AsNoTracking()
            .SingleAsync(row => row.Id == original.Id, Ct);
        Assert.Equal(original.Version, stored.Version);
        Assert.Equal(original.LinksJson, stored.LinksJson);
        Assert.Equal(original.Text, stored.Text);
        Assert.Equal(original.PositiveFeedback, stored.PositiveFeedback);
    }

    private static AssistantLinkResponse Link(AssistantRouteKey route, Guid? tenant = null, Guid? company = null) =>
        new(route, new(null, null, company, tenant, null), new(null, null, null, null), "Test link");

    private sealed class ProfileProbe
    {
        internal int Calls { get; set; }
        internal bool Fail { get; set; }
    }

    private sealed class ObservedProfile(IProfileService inner, ProfileProbe probe) : IProfileService
    {
        public Task<Result<MeResponse>> GetAsync(CancellationToken cancellationToken)
        {
            probe.Calls++;
            return probe.Fail ? Task.FromResult(Result.Failure<MeResponse>(UserErrors.NotFound)) : inner.GetAsync(cancellationToken);
        }

        public Task<Result<VersionResponse>> UpdateAsync(UpdateMeRequest request, CancellationToken cancellationToken) =>
            inner.UpdateAsync(request, cancellationToken);
    }
}
