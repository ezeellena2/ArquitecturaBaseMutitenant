using System.Net;
using System.Text.Json;
using System.Net.Http.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantDurableConversationTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    [Fact]
    public async Task Quick_snapshot_survives_rename_feedback_global_expiry_and_requires_current_notice()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client);
        var key = Guid.NewGuid();
        using var result = await PostAsync(client, "/api/me/assistant/quick/my_signin", "{}", key);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var body = await result.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain(account.Email.Value, body, StringComparison.Ordinal);
        var quick = JsonSerializer.Deserialize<QuickRead>(body, Json)!;
        Assert.NotEqual(0u, quick.Conversation.Version);
        Assert.All(quick.Messages, message => Assert.Equal(quick.Conversation.Version, message.Version));
        using var rename = await client.PutAsJsonAsync($"/api/me/assistant/conversations/{quick.Conversation.Id}", new { title = "Mi título manual", isSaved = true, version = quick.Conversation.Version }, Ct);
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        var renamed = (await rename.Content.ReadFromJsonAsync<Application.Models.Common.VersionResponse>(Ct))!;
        Assert.NotEqual(quick.Conversation.Version, renamed.Version);
        var beforeToggle = await client.GetFromJsonAsync<AssistantConversationResponse>($"/api/me/assistant/conversations/{quick.Conversation.Id}", Json, Ct);
        using var toggle = await client.PutAsJsonAsync($"/api/me/assistant/conversations/{quick.Conversation.Id}", new { title = "  ", isSaved = false, version = renamed.Version }, Ct);
        Assert.Equal(HttpStatusCode.OK, toggle.StatusCode);
        var afterToggle = await client.GetFromJsonAsync<AssistantConversationResponse>($"/api/me/assistant/conversations/{quick.Conversation.Id}", Json, Ct);
        Assert.Equal("Mi título manual", afterToggle!.Conversation.Title);
        Assert.Equal(beforeToggle!.Conversation.LastActivityAtUtc, afterToggle.Conversation.LastActivityAtUtc);
        Assert.False(afterToggle.Conversation.IsSaved);
        using var nextQuick = await PostAsync(client, "/api/me/assistant/quick/my_signin", JsonSerializer.Serialize(new { conversationId = quick.Conversation.Id, version = afterToggle.Conversation.Version }), Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, nextQuick.StatusCode);
        var manual = JsonSerializer.Deserialize<QuickRead>(await nextQuick.Content.ReadAsStringAsync(Ct), Json)!;
        Assert.Equal("Mi título manual", manual.Conversation.Title);
        using var stale = await client.PutAsJsonAsync($"/api/me/assistant/conversations/{quick.Conversation.Id}", new { isSaved = false, version = quick.Conversation.Version }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var message = quick.Messages.Single(row => row.Role == AssistantMessageRole.Assistant);
        using var feedback = await client.PutAsJsonAsync($"/api/me/assistant/conversations/{quick.Conversation.Id}/messages/{message.Id}/feedback", new { rating = "up", version = message.Version }, Ct);
        Assert.Equal(HttpStatusCode.OK, feedback.StatusCode);
        var rated = (await feedback.Content.ReadFromJsonAsync<Application.Models.Common.VersionResponse>(Ct))!;
        Assert.NotEqual(message.Version, rated.Version);
        using var staleFeedback = await client.PutAsJsonAsync($"/api/me/assistant/conversations/{quick.Conversation.Id}/messages/{message.Id}/feedback", new { rating = "down", version = message.Version }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, staleFeedback.StatusCode);
        var afterFeedback = await client.GetFromJsonAsync<AssistantConversationResponse>($"/api/me/assistant/conversations/{quick.Conversation.Id}", Json, Ct);
        Assert.Equal(rated.Version, afterFeedback!.Messages.Items.Single(row => row.Id == message.Id).Version);
        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var db = services.GetRequiredService<ApplicationDbContext>();
                var global = await db.Set<IdempotencyKey>().SingleAsync(row => row.UserId == account.UserId && row.Key == key, ct);
                Assert.Null(global.ResponseBody);
                Assert.Equal("assistant.quick", global.ReferenceKind);
                db.Entry(global).Property(row => row.ExpiresAtUtc).CurrentValue = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddDays(-2);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        using var replay = await PostAsync(client, "/api/me/assistant/quick/my_signin", "{}", key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(body, await replay.Content.ReadAsStringAsync(Ct));
        using var changedRoute = await PostAsync(client, "/api/me/assistant/quick/my_organizations", "{}", key);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, changedRoute.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var db = services.GetRequiredService<ApplicationDbContext>();
                db.RemoveRange(await db.Set<AssistantNoticeAcceptance>().Where(row => row.OwnerUserId == account.UserId).ToArrayAsync(ct));
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        using var denied = await PostAsync(client, "/api/me/assistant/quick/my_signin", "{}", key);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
    }

    [Fact]
    public async Task Rename_normalizes_NFC_bounds_eighty_and_foreign_owner_cannot_mutate_or_append()
    {
        using var client = AssistantTestData.Client(factory);
        await AccountJourney.RegisterAsync(factory, client, Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client);
        using var created = await PostAsync(client, "/api/me/assistant/conversations", "{}", Guid.NewGuid());
        var row = (await created.Content.ReadFromJsonAsync<AssistantConversationSummaryResponse>(Ct))!;
        using var tooLong = await client.PutAsJsonAsync($"/api/me/assistant/conversations/{row.Id}", new { title = new string('a', 81), isSaved = true, version = row.Version }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        var title = "Cafe\u0301" + new string('a', 76);
        using var renamed = await client.PutAsJsonAsync($"/api/me/assistant/conversations/{row.Id}", new { title, isSaved = true, version = row.Version }, Ct);
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var read = (await client.GetFromJsonAsync<AssistantConversationResponse>($"/api/me/assistant/conversations/{row.Id}", Json, Ct))!;
        Assert.Equal(title.Normalize(), read.Conversation.Title);
        Assert.Equal(80, read.Conversation.Title!.Length);
        using var foreign = AssistantTestData.Client(factory);
        await AccountJourney.RegisterAsync(factory, foreign, Ct);
        await AssistantExecutionActorTests.AcceptNoticeAsync(foreign);
        using var update = await foreign.PutAsJsonAsync($"/api/me/assistant/conversations/{row.Id}", new { title = "Ajeno", isSaved = false, version = read.Conversation.Version }, Ct);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        using var delete = await foreign.DeleteAsync($"/api/me/assistant/conversations/{row.Id}?version={read.Conversation.Version}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        using var append = await PostAsync(foreign, "/api/me/assistant/quick/my_signin", JsonSerializer.Serialize(new { conversationId = row.Id, version = read.Conversation.Version }), Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, append.StatusCode);
    }

    [Fact]
    public async Task Fixed_clock_orders_repeated_Quick_pairs_and_history_cursor_is_bound_to_conversation()
    {
        var clock = new FakeTimeProvider(factory.Services.GetRequiredService<TimeProvider>().GetUtcNow());
        var provider = new AssistantDurableAuthorizationTests.NoChat();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock));
            services.Replace(ServiceDescriptor.Singleton<Application.Modules.Assistant.Interfaces.Integrations.IChatClientFactory>(provider));
        }));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await AccountJourney.RegisterAsync(factory, client, Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client);
        QuickRead? quick = null;
        var originalIds = new List<Guid>();
        for (var index = 0; index < 26; index++)
        {
            var input = quick is null ? "{}" : JsonSerializer.Serialize(new { conversationId = quick.Conversation.Id, version = quick.Conversation.Version });
            using var result = await PostAsync(client, "/api/me/assistant/quick/my_signin", input, Guid.NewGuid());
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            quick = JsonSerializer.Deserialize<QuickRead>(await result.Content.ReadAsStringAsync(Ct), Json)!;
            originalIds.AddRange(quick.Messages.Select(row => row.Id));
        }
        var first = await client.GetFromJsonAsync<AssistantConversationResponse>($"/api/me/assistant/conversations/{quick!.Conversation.Id}", Json, Ct);
        Assert.NotNull(first);
        Assert.Equal(50, first.Messages.Items.Count);
        Assert.True(first.Messages.HasMore);
        var second = await client.GetFromJsonAsync<AssistantConversationResponse>($"/api/me/assistant/conversations/{quick.Conversation.Id}?cursor={Uri.EscapeDataString(first.Messages.NextCursor!)}", Json, Ct);
        Assert.Equal(originalIds, first.Messages.Items.Concat(second!.Messages.Items).Select(row => row.Id));
        Assert.All(first.Messages.Items.Chunk(2), pair =>
        {
            Assert.Equal(AssistantMessageRole.User, pair[0].Role);
            Assert.Equal(AssistantMessageRole.Assistant, pair[1].Role);
            Assert.True(pair[0].CreatedAtUtc < pair[1].CreatedAtUtc);
        });
        using var other = await PostAsync(client, "/api/me/assistant/conversations", "{}", Guid.NewGuid());
        var otherConversation = await other.Content.ReadFromJsonAsync<AssistantConversationSummaryResponse>(Ct);
        using var rejected = await client.GetAsync($"/api/me/assistant/conversations/{otherConversation!.Id}?cursor={Uri.EscapeDataString(first.Messages.NextCursor!)}", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task Create_replays_the_original_id_and_location_and_deleted_content_is_not_recreated()
    {
        using var client = AssistantTestData.Client(factory);
        await AccountJourney.RegisterAsync(factory, client, Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client);
        var key = Guid.NewGuid();
        using var created = await PostAsync(client, "/api/me/assistant/conversations", "{}", key);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadAsStringAsync(Ct);
        using var value = JsonDocument.Parse(body);
        var id = value.RootElement.GetProperty("id").GetGuid();
        var version = value.RootElement.GetProperty("version").GetUInt32();
        Assert.NotEqual(0u, version);
        Assert.Contains(id.ToString("D"), created.Headers.Location!.ToString(), StringComparison.Ordinal);
        using var replay = await PostAsync(client, "/api/me/assistant/conversations", "{}", key);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(body, await replay.Content.ReadAsStringAsync(Ct));
        Assert.Equal(created.Headers.Location, replay.Headers.Location);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotent-Replayed")));
        using var deleted = await client.DeleteAsync($"/api/me/assistant/conversations/{id}?version={version}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var missing = await PostAsync(client, "/api/me/assistant/conversations", "{}", key);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var reused = await PostAsync(client, "/api/me/assistant/conversations", "{ }", key);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused.StatusCode);
    }

    internal static Task<HttpResponseMessage> PostAsync(HttpClient client, string route, string body, Guid key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        request.Headers.Add("Idempotency-Key", key.ToString("D"));
        return client.SendAsync(request, Ct);
    }
    private sealed record QuickRead(AssistantConversationSummaryResponse Conversation, IReadOnlyList<AssistantMessageResponse> Messages);
}
