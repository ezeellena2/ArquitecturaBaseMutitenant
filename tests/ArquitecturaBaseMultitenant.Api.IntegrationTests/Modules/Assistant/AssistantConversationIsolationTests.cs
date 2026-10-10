using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantConversationIsolationTests(ApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter<AssistantMessageRole>(), new JsonStringEnumConverter<AssistantMessageStatus>(), new JsonStringEnumConverter<AssistantRating>() },
    };
    [Fact]
    public async Task Message_pages_are_chronological_default_to_fifty_and_bind_the_cursor_to_the_conversation()
    {
        await AssistantTestData.PersonalAsync(factory, true);
        using var client = AssistantTestData.Client(factory);
        var person = await AccountJourney.RegisterAsync(factory, client, AssistantTestData.Ct);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client);
        var tenantId = await AssistantTestData.ActiveTenantAsync(client);
        var conversation = await AssistantTestData.ConversationAsync(factory, tenantId, person.UserId, Access.Consumer);
        var sibling = await AssistantTestData.ConversationAsync(factory, tenantId, person.UserId, Access.Consumer);
        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                for (var index = 0; index < 51; index++)
                    services.GetRequiredService<ApplicationDbContext>().Add(AssistantMessage.Create(conversation.Id, "User", "Complete", $"fixture-{index}", conversation.CreatedAtUtc.AddSeconds(index)));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, AssistantTestData.Ct);
        }
        var page = await client.GetFromJsonAsync<AssistantConversationResponse>($"/api/me/assistant/conversations/{conversation.Id}", Json, AssistantTestData.Ct);
        Assert.Equal(50, page!.Messages.Items.Count);
        Assert.Equal("fixture-0", page.Messages.Items[0].Text);
        Assert.Equal("fixture-49", page.Messages.Items[^1].Text);
        Assert.True(page.Messages.HasMore);
        var cursor = Uri.EscapeDataString(page.Messages.NextCursor!);
        var last = await client.GetFromJsonAsync<AssistantConversationResponse>($"/api/me/assistant/conversations/{conversation.Id}?cursor={cursor}", Json, AssistantTestData.Ct);
        Assert.Equal("fixture-50", Assert.Single(last!.Messages.Items).Text);
        Assert.False(last.Messages.HasMore);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/me/assistant/conversations/{sibling.Id}?cursor={cursor}", AssistantTestData.Ct)).StatusCode);
        await AssistantTestData.PersonalAsync(factory, false);
    }
    [Fact]
    public async Task Two_people_two_organizations_and_full_owners_cannot_read_another_owner_or_access_chat()
    {
        await AssistantTestData.PersonalAsync(factory, true);
        using var a = AssistantTestData.Client(factory); using var b = AssistantTestData.Client(factory);
        var personA = await AccountJourney.RegisterAsync(factory, a, AssistantTestData.Ct);
        await AssistantExecutionActorTests.AcceptNoticeAsync(a);
        var personalA = await AssistantTestData.ActiveTenantAsync(a);
        var personB = await AccountJourney.RegisterAsync(factory, b, AssistantTestData.Ct);
        var personalB = await AssistantTestData.ActiveTenantAsync(b);
        var chatA = await AssistantTestData.ConversationAsync(factory, personalA, personA.UserId, Access.Consumer);
        var chatB = await AssistantTestData.ConversationAsync(factory, personalB, personB.UserId, Access.Consumer);
        var wrongPersonalAccess = await AssistantTestData.ConversationAsync(factory, personalA, personA.UserId, Access.Business);
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync($"/api/me/assistant/conversations/{chatA.Id}", AssistantTestData.Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/me/assistant/conversations/{chatB.Id}", AssistantTestData.Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/me/assistant/conversations/{wrongPersonalAccess.Id}", AssistantTestData.Ct)).StatusCode);
        var orgA = await OrganizationJourney.StageAsync(factory, personA, "Assistant A", AssistantTestData.Ct);
        var orgB = await OrganizationJourney.StageAsync(factory, personB, "Assistant B", AssistantTestData.Ct);
        await OrganizationJourney.AddActiveMemberAsync(factory, orgA, personB.UserId, AssistantTestData.Ct);
        await AssistantTestData.EnableAsync(factory, orgA); await AssistantTestData.EnableAsync(factory, orgB);
        var businessA = await AssistantTestData.ConversationAsync(factory, orgA, personA.UserId, Access.Business);
        var colleague = await AssistantTestData.ConversationAsync(factory, orgA, personB.UserId, Access.Business);
        var businessB = await AssistantTestData.ConversationAsync(factory, orgB, personB.UserId, Access.Business);
        using var c = AssistantTestData.Client(factory);
        var personC = await AccountJourney.RegisterAsync(factory, c, AssistantTestData.Ct);
        await OrganizationJourney.AddActiveMemberAsync(factory, orgA, personC.UserId, AssistantTestData.Ct);
        var secondColleague = await AssistantTestData.ConversationAsync(factory, orgA, personC.UserId, Access.Business);
        var wrongBusinessAccess = await AssistantTestData.ConversationAsync(factory, orgA, personA.UserId, Access.Consumer);
        await AccountJourney.AuthorizeAsync(a, "business", AssistantTestData.Ct, orgA);
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync($"/api/organization/assistant/conversations/{businessA.Id}", AssistantTestData.Ct)).StatusCode);
        foreach (var id in new[] { colleague.Id, secondColleague.Id, businessB.Id, chatA.Id, chatB.Id, wrongBusinessAccess.Id })
            Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/organization/assistant/conversations/{id}", AssistantTestData.Ct)).StatusCode);
        using var list = await a.GetAsync("/api/organization/assistant/conversations", AssistantTestData.Ct);
        var page = await list.Content.ReadFromJsonAsync<ArquitecturaBaseMultitenant.Application.Common.Pagination.CursorResult<AssistantConversationSummaryResponse>>(AssistantTestData.Ct);
        Assert.Equal(businessA.Id, Assert.Single(page!.Items).Id);
        await AccountJourney.AuthorizeAsync(a, "consumer", AssistantTestData.Ct);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/me/assistant/conversations/{businessA.Id}", AssistantTestData.Ct)).StatusCode);
        await AssistantTestData.PersonalAsync(factory, false);
    }

    [Fact]
    public async Task Operators_with_every_permission_have_only_their_own_global_content()
    {
        using var a = AssistantTestData.Client(factory); using var b = AssistantTestData.Client(factory);
        var operatorA = await AccountJourney.RegisterAsync(factory, a, AssistantTestData.Ct);
        var operatorB = await AccountJourney.RegisterAsync(factory, b, AssistantTestData.Ct);
        await PlatformOperators.GrantAsync(factory.Services, operatorA.UserId, PlatformOperators.All, AssistantTestData.Ct);
        await PlatformOperators.GrantAsync(factory.Services, operatorB.UserId, PlatformOperators.All, AssistantTestData.Ct);
        var own = PlatformAssistantConversation.Create(operatorA.UserId, factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
        var ownSecond = PlatformAssistantConversation.Create(operatorA.UserId, own.CreatedAtUtc);
        var ownThird = PlatformAssistantConversation.Create(operatorA.UserId, own.CreatedAtUtc);
        var foreign = PlatformAssistantConversation.Create(operatorB.UserId, factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
        var foreignFile = PlatformAssistantAttachment.Create(foreign.Id, "private-fixture.txt", "text/plain", "fixture-private", 1, factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); db.Add(own); db.Add(ownSecond); db.Add(ownThird); db.Add(foreign); db.Add(foreignFile);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, AssistantTestData.Ct);
        await AccountJourney.AuthorizeAsync(a, "platform", AssistantTestData.Ct);
        await AssistantExecutionActorTests.AcceptNoticeAsync(a, "/api/platform/assistant/notice");
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync($"/api/platform/assistant/conversations/{own.Id}", AssistantTestData.Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/platform/assistant/conversations/{foreign.Id}", AssistantTestData.Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/platform/assistant/conversations/{foreign.Id}/attachments/{foreignFile.Id}", AssistantTestData.Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/platform/assistant/conversations/{own.Id}/attachments/{foreignFile.Id}", AssistantTestData.Ct)).StatusCode);
        var first = await a.GetFromJsonAsync<ArquitecturaBaseMultitenant.Application.Common.Pagination.CursorResult<AssistantConversationSummaryResponse>>("/api/platform/assistant/conversations?limit=2", AssistantTestData.Ct);
        Assert.True(first!.HasMore);
        Assert.Equal(2, first.Items.Count);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.GetAsync($"/api/platform/assistant/conversations?saved=true&cursor={Uri.EscapeDataString(first.NextCursor!)}", AssistantTestData.Ct)).StatusCode);
        await AccountJourney.AuthorizeAsync(b, "platform", AssistantTestData.Ct);
        await AssistantExecutionActorTests.AcceptNoticeAsync(b, "/api/platform/assistant/notice");
        Assert.Equal(HttpStatusCode.BadRequest, (await b.GetAsync($"/api/platform/assistant/conversations?cursor={Uri.EscapeDataString(first.NextCursor!)}", AssistantTestData.Ct)).StatusCode);
        var last = await a.GetFromJsonAsync<ArquitecturaBaseMultitenant.Application.Common.Pagination.CursorResult<AssistantConversationSummaryResponse>>($"/api/platform/assistant/conversations?limit=2&cursor={Uri.EscapeDataString(first.NextCursor!)}", AssistantTestData.Ct);
        Assert.False(last!.HasMore);
        Assert.Single(last.Items);
        Assert.Equal(new[] { own.Id, ownSecond.Id, ownThird.Id }.Order(), first.Items.Concat(last.Items).Select(value => value.Id).Order());
    }
}
