using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Idempotency;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantDurableTransportTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Failure_after_staging_private_binding_rolls_back_the_whole_effect_and_releases_transport()
    {
        var fault = new WriteFault();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            var original = services.Single(row => row.ServiceType == typeof(IAssistantDurableRepository));
            services.Replace(ServiceDescriptor.Scoped<IAssistantDurableRepository>(provider => new FaultRepository(
                (IAssistantDurableRepository)ActivatorUtilities.CreateInstance(provider, original.ImplementationType!), fault)));
        }));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client);
        var tenantId = await AssistantTestData.ActiveTenantAsync(client);
        var key = Guid.NewGuid();
        using var failed = await AssistantDurableConversationTests.PostAsync(client, "/api/me/assistant/quick/my_signin", "{}", key);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Empty(await db.Set<AssistantConversation>().Where(row => row.OwnerUserId == account.UserId).ToArrayAsync(Ct));
            Assert.Empty(await db.Set<AssistantDurableOperation>().Where(row => row.OwnerUserId == account.UserId).ToArrayAsync(Ct));
            Assert.Empty(await db.Set<IdempotencyKey>().Where(row => row.UserId == account.UserId && row.Key == key).ToArrayAsync(Ct));
        }
        fault.Fail = false;
        using var recovered = await AssistantDurableConversationTests.PostAsync(client, "/api/me/assistant/quick/my_signin", "{}", key);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Private_commit_recovers_after_global_complete_failure_or_client_abort(bool abort)
    {
        var faults = new TransportFault { PauseComplete = abort, FailComplete = !abort };
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IIdempotencyStore>(provider => new FaultStore(
                new IdempotencyStore(factory.RuntimeConnectionString, provider.GetRequiredService<TimeProvider>()), faults)))));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client);
        faults.Armed = true;
        var key = Guid.NewGuid();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/me/assistant/quick/my_signin") { Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json") };
        request.Headers.Add("Idempotency-Key", key.ToString("D"));
        var pending = client.SendAsync(request, cancellation.Token);
        if (abort)
        {
            await faults.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20), Ct);
            cancellation.Cancel();
            faults.Resume.TrySetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { using var response = await pending; });
        }
        else
        {
            using var failed = await pending;
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        }
        faults.Armed = false;
        using var replay = await AssistantDurableConversationTests.PostAsync(client, "/api/me/assistant/quick/my_signin", "{}", key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotent-Replayed")));
        using var value = JsonDocument.Parse(await replay.Content.ReadAsStringAsync(Ct));
        var id = value.RootElement.GetProperty("conversation").GetProperty("id").GetGuid();
        using var scope = factory.Services.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(await AssistantTestData.ActiveTenantAsync(client));
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Single(await db.Set<AssistantDurableOperation>().Where(row => row.OwnerUserId == account.UserId && row.Key == key).ToArrayAsync(Ct));
        Assert.Equal(2, await db.Set<AssistantMessage>().CountAsync(row => row.ConversationId == id, Ct));
        Assert.Equal(id, faults.Reference is null ? Guid.Empty :
            (await db.Set<AssistantDurableOperation>().SingleAsync(row => row.Id == faults.Reference.Id, Ct)).ConversationId);
    }

    [Fact]
    public async Task Two_hosts_arbitrate_same_key_and_different_body_while_work_is_in_progress()
    {
        var faults = new TransportFault { PauseReserve = true };
        using var one = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IIdempotencyStore>(provider => new FaultStore(
                new IdempotencyStore(factory.RuntimeConnectionString, provider.GetRequiredService<TimeProvider>()), faults)))));
        using var two = factory.WithWebHostBuilder(_ => { });
        using var a = one.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var b = two.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, a, Ct);
        using (var setup = factory.Services.CreateScope())
        {
            var services = setup.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var db = services.GetRequiredService<ApplicationDbContext>();
                foreach (var consumed in await db.LoginCodes.Where(row => row.Destination == account.Email.Value && row.ConsumedAtUtc != null).ToArrayAsync(ct))
                    db.Entry(consumed).Property(row => row.CreatedAtUtc).CurrentValue = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddHours(-1);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        // Testing hosts generate their own transport keys; obtain a real token in the second host.
        using var requested = await AccountJourney.PostAsync(b, "/api/auth/login-code", new { email = account.Email.Value }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(two.Services, account.Email.Value, Ct);
        using var verified = await AccountJourney.PostAsync(b, "/api/auth/login-code/verify", new { email = account.Email.Value, code, returnUrl = "/invitacion" }, Ct);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        await AccountJourney.AuthorizeAsync(b, "consumer", Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        await AssistantExecutionActorTests.AcceptNoticeAsync(a);
        faults.Armed = true;
        var key = Guid.NewGuid();
        var pending = AssistantDurableConversationTests.PostAsync(a, "/api/me/assistant/conversations", "{}", key);
        await faults.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20), Ct);
        try
        {
            using var same = await AssistantDurableConversationTests.PostAsync(b, "/api/me/assistant/conversations", "{}", key);
            Assert.Equal(HttpStatusCode.Conflict, same.StatusCode);
            using var changed = await AssistantDurableConversationTests.PostAsync(b, "/api/me/assistant/conversations", "{ }", key);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, changed.StatusCode);
        }
        finally { faults.Resume.TrySetResult(); }
        using var created = await pending;
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        faults.Armed = false;
        using var replay = await AssistantDurableConversationTests.PostAsync(b, "/api/me/assistant/conversations", "{}", key);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(await created.Content.ReadAsStringAsync(Ct), await replay.Content.ReadAsStringAsync(Ct));
        using var scope = factory.Services.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(await AssistantTestData.ActiveTenantAsync(a));
        Assert.Single(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantConversation>().Where(row => row.OwnerUserId == account.UserId).ToArrayAsync(Ct));
    }

    private sealed class TransportFault
    {
        internal bool Armed { get; set; }
        internal bool FailComplete { get; init; }
        internal bool PauseComplete { get; init; }
        internal bool PauseReserve { get; init; }
        internal IdempotentReference? Reference { get; set; }
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class WriteFault { internal bool Fail { get; set; } = true; }
    private sealed record FaultRepository(IAssistantDurableRepository Inner, WriteFault Fault) : IAssistantDurableRepository
    {
        public Task LockAsync(Guid owner, Access access, Guid key, Guid? conversation, CancellationToken ct) => Inner.LockAsync(owner, access, key, conversation, ct);
        public Task<AssistantDurableOperationRow?> GetByKeyAsync(Guid owner, Access access, Guid key, CancellationToken ct) => Inner.GetByKeyAsync(owner, access, key, ct);
        public Task<AssistantDurableOperationRow?> GetByIdAsync(Guid owner, Access access, Guid id, CancellationToken ct) => Inner.GetByIdAsync(owner, access, id, ct);
        public Entity Add(Guid owner, Access access, IdempotentRequest request, Guid conversation, IdempotentReference reference, string snapshot, string? quickKey, DateTime nowUtc)
        {
            var row = Inner.Add(owner, access, request, conversation, reference, snapshot, quickKey, nowUtc);
            if (Fault.Fail) throw new InvalidOperationException("Fixture failure after staging the private effect.");
            return row;
        }
        public Task RemoveContentAsync(Guid owner, Access access, Guid conversation, CancellationToken ct) => Inner.RemoveContentAsync(owner, access, conversation, ct);
        public Task StageFileCleanupAsync(Guid owner, Access access, Guid conversation, DateTime nowUtc, CancellationToken ct) => Inner.StageFileCleanupAsync(owner, access, conversation, nowUtc, ct);
    }

    private sealed record FaultStore(IIdempotencyStore Inner, TransportFault Fault) : IIdempotencyStore
    {
        public async Task<IdempotencyReservation> ReserveAsync(Guid? tenant, Guid user, Guid key, string hash, string route, CancellationToken ct)
        {
            var result = await Inner.ReserveAsync(tenant, user, key, hash, route, ct);
            if (Fault.Armed && Fault.PauseReserve && result.State == IdempotencyReservationState.Reserved)
            {
                Fault.Entered.TrySetResult();
                await Fault.Resume.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
            }
            return result;
        }
        public Task CompleteAsync(Guid id, int status, string body, CancellationToken ct) => Inner.CompleteAsync(id, status, body, ct);
        public async Task CompleteReferenceAsync(Guid id, IdempotentReference reference, CancellationToken ct)
        {
            Fault.Reference = reference;
            if (Fault.Armed && Fault.FailComplete) throw new InvalidOperationException("Fixture publication failure after private commit.");
            if (Fault.Armed && Fault.PauseComplete)
            {
                Fault.Entered.TrySetResult();
                await Fault.Resume.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
            }
            await Inner.CompleteReferenceAsync(id, reference, ct);
        }
        public Task ReleaseAsync(Guid id, CancellationToken ct) => Inner.ReleaseAsync(id, ct);
        public Task<int> CleanupExpiredAsync(CancellationToken ct) => Inner.CleanupExpiredAsync(ct);
    }
}
