using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Idempotency;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantSnapshotReplayRetentionTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public async Task Expired_original_never_repeats_effect_after_global_binding_is_removed(bool platform, bool quick, bool expireDuringReplay)
    {
        // Freeze the clock and age only the durable fixture; avoid expiring the login session or changing legal fixtures.
        var initialUtc = factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var clock = new FakeTimeProvider(new DateTimeOffset(initialUtc.AddTicks(-(initialUtc.Ticks % 10))));
        var pause = new ReplayPause();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock));
            services.Replace(ServiceDescriptor.Scoped<IUserStatusReader>(provider => new PausedIdentityReader(
                ActivatorUtilities.CreateInstance<UserStatusReader>(provider), pause)));
        }));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        if (platform)
        {
            await PlatformOperators.GrantAsync(host.Services, account.UserId, PlatformOperators.All, Ct);
            await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        }
        else await AssistantTestData.PersonalAsync(factory, true);
        var prefix = platform ? "/api/platform/assistant" : "/api/me/assistant";
        await AssistantExecutionActorTests.AcceptNoticeAsync(client, prefix + "/notice");
        var route = prefix + (quick ? "/quick/" + (platform ? "today_security" : "my_signin") : "/conversations");
        var key = Guid.NewGuid();
        using var created = await AssistantDurableConversationTests.PostAsync(client, route, "{}", key);
        Assert.Equal(quick ? HttpStatusCode.OK : HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadAsStringAsync(Ct);
        using var payload = JsonDocument.Parse(body);
        var conversationId = (quick ? payload.RootElement.GetProperty("conversation") : payload.RootElement).GetProperty("id").GetGuid();
        var tenantId = platform ? (Guid?)null : await AssistantTestData.ActiveTenantAsync(client);

        using (var scope = host.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            using var tenant = tenantId is { } id ? services.GetRequiredService<ITenantScope>().Enter(id) : null;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var db = services.GetRequiredService<ApplicationDbContext>();
                if (platform)
                {
                    var row = await db.Set<PlatformAssistantDurableOperation>().SingleAsync(value => value.OwnerUserId == account.UserId && value.Key == key, ct);
                    row.PreserveSnapshotVersion();
                    db.Entry(row).Property(value => value.CreatedAtUtc).CurrentValue = clock.GetUtcNow().UtcDateTime.AddHours(-24).AddTicks(10);
                }
                else
                {
                    var row = await db.Set<AssistantDurableOperation>().SingleAsync(value => value.OwnerUserId == account.UserId && value.Key == key, ct);
                    row.PreserveSnapshotVersion();
                    db.Entry(row).Property(value => value.CreatedAtUtc).CurrentValue = clock.GetUtcNow().UtcDateTime.AddHours(-24).AddTicks(10);
                }
                db.RemoveRange(await db.Set<IdempotencyKey>().Where(value => value.UserId == account.UserId && value.Key == key).ToArrayAsync(ct));
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        using var live = await AssistantDurableConversationTests.PostAsync(client, route, "{}", key);
        Assert.Equal(created.StatusCode, live.StatusCode);
        Assert.Equal(body, await live.Content.ReadAsStringAsync(Ct));
        if (expireDuringReplay)
        {
            pause.Armed = true;
            var pending = AssistantDurableConversationTests.PostAsync(client, route, "{}", key);
            try
            {
                await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20), Ct);
                clock.Advance(TimeSpan.FromTicks(10));
            }
            finally { pause.Resume.TrySetResult(); }
            using var expiredWhileWaiting = await pending;
            Assert.Equal(HttpStatusCode.NotFound, expiredWhileWaiting.StatusCode);
            pause.Armed = false;
        }
        else clock.Advance(TimeSpan.FromTicks(10));
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var expired = await AssistantDurableConversationTests.PostAsync(client, route, "{}", key);
            Assert.Equal(HttpStatusCode.NotFound, expired.StatusCode);
            Assert.DoesNotContain(body, await expired.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        }
        using var reused = await AssistantDurableConversationTests.PostAsync(client, route, "{ }", key);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused.StatusCode);
        using var changedRoute = await AssistantDurableConversationTests.PostAsync(client, prefix + (quick ? "/conversations" : "/quick/" + (platform ? "today_security" : "my_signin")), "{}", key);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, changedRoute.StatusCode);
        using var readScope = host.Services.CreateScope();
        var read = readScope.ServiceProvider;
        using var readTenant = tenantId is { } scopeId ? read.GetRequiredService<ITenantScope>().Enter(scopeId) : null;
        var database = read.GetRequiredService<ApplicationDbContext>();
        if (platform)
        {
            Assert.Single(await database.Set<PlatformAssistantConversation>().Where(value => value.OwnerUserId == account.UserId).ToArrayAsync(Ct));
            Assert.Single(await database.Set<PlatformAssistantDurableOperation>().Where(value => value.OwnerUserId == account.UserId && value.Key == key).ToArrayAsync(Ct));
            Assert.Equal(quick ? 2 : 0, await database.Set<PlatformAssistantMessage>().CountAsync(value => value.ConversationId == conversationId, Ct));
        }
        else
        {
            Assert.Single(await database.Set<AssistantConversation>().Where(value => value.OwnerUserId == account.UserId).ToArrayAsync(Ct));
            Assert.Single(await database.Set<AssistantDurableOperation>().Where(value => value.OwnerUserId == account.UserId && value.Key == key).ToArrayAsync(Ct));
            Assert.Equal(quick ? 2 : 0, await database.Set<AssistantMessage>().CountAsync(value => value.ConversationId == conversationId, Ct));
        }
    }

    [Fact]
    public async Task Quick_is_composed_once_outside_transaction_and_replay_never_composes_again()
    {
        var calls = new QuickCalls();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Scoped<IAssistantQuickService>(provider => new TransactionCheckingQuick(
                ActivatorUtilities.CreateInstance<AssistantQuickService>(provider), provider.GetRequiredService<ApplicationDbContext>(), calls)))));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await AccountJourney.RegisterAsync(host, client, Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client);
        var key = Guid.NewGuid();
        using var first = await AssistantDurableConversationTests.PostAsync(client, "/api/me/assistant/quick/my_signin", "{}", key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var body = await first.Content.ReadAsStringAsync(Ct);
        using var replay = await AssistantDurableConversationTests.PostAsync(client, "/api/me/assistant/quick/my_signin", "{}", key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(body, await replay.Content.ReadAsStringAsync(Ct));
        Assert.Equal(1, calls.Count);
    }

    private sealed class QuickCalls { internal int Count; }
    private sealed class ReplayPause
    {
        internal bool Armed;
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private sealed record PausedIdentityReader(IUserStatusReader Inner, ReplayPause Pause) : IUserStatusReader
    {
        public Task<UserStatus?> GetStatusAsync(Guid id, CancellationToken ct) => Inner.GetStatusAsync(id, ct);
        public Task<IReadOnlyCollection<Guid>> ListActiveAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Inner.ListActiveAsync(ids, ct);
        public Task<Result<IReadOnlyList<UserDeletionStateRow>>> LockDeletionStatesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Inner.LockDeletionStatesAsync(ids, ct);
        public async Task<Result<IReadOnlyList<UserDeletionStateRow>>> ListDeletionStatesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
        {
            if (Pause.Armed)
            {
                Pause.Entered.TrySetResult();
                await Pause.Resume.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
            }
            return await Inner.ListDeletionStatesAsync(ids, ct);
        }
    }
    private sealed record TransactionCheckingQuick(IAssistantQuickService Inner, ApplicationDbContext Database, QuickCalls Calls) : IAssistantQuickService
    {
        public Task<Result<IReadOnlyList<AssistantQuickQueryResponse>>> ListAsync(CancellationToken ct) => Inner.ListAsync(ct);
        public Task<Result<AssistantQuickCompositionResponse>> ExecuteAsync(string key, CancellationToken ct)
        {
            Assert.Null(Database.Database.CurrentTransaction);
            Interlocked.Increment(ref Calls.Count);
            return Inner.ExecuteAsync(key, ct);
        }
    }
}
