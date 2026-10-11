using System.Net;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

public sealed class AssistantSnapshotConcurrentDeletionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Two_real_processors_hold_both_target_identities_and_purge_mutual_copies_without_foreign_shares_or_lock_cycles(
        bool releaseSecondFirst, bool releaseTogether)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        var ct = deadline.Token;
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        var rendezvous = new DeletionRendezvous();
        using var host = isolated.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Insert(0, ServiceDescriptor.Singleton<IAccountDeletionParticipant>(rendezvous))));
        using var firstClient = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var secondClient = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var ownerClient = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var first = await AccountJourney.RegisterAsync(host, firstClient, ct);
        var second = await AccountJourney.RegisterAsync(host, secondClient, ct);
        var owner = await AccountJourney.RegisterAsync(host, ownerClient, ct);
        var tenantId = await OrganizationJourney.StageAsync(isolated, owner, "Mutual snapshot deletion", ct);
        await OrganizationJourney.AddActiveMemberAsync(isolated, tenantId, first.UserId, ct);
        await OrganizationJourney.AddActiveMemberAsync(isolated, tenantId, second.UserId, ct);
        rendezvous.Configure(first.UserId, second.UserId, tenantId);
        var nowUtc = host.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var firstCopy = AssistantDurableOperation.Create(first.UserId, Access.Business, Guid.CreateVersion7(), "first", "/mutual/first", Guid.CreateVersion7(),
            "assistant.quick", 200, null, "{\"name\":\"second copied identity\"}", "active_users", nowUtc, [second.UserId]);
        var secondCopy = AssistantDurableOperation.Create(second.UserId, Access.Business, Guid.CreateVersion7(), "second", "/mutual/second", Guid.CreateVersion7(),
            "assistant.quick", 200, null, "{\"name\":\"first copied identity\"}", "active_users", nowUtc, [first.UserId]);
        var thirdCopy = AssistantDurableOperation.Create(owner.UserId, Access.Business, Guid.CreateVersion7(), "third", "/mutual/third", Guid.CreateVersion7(),
            "assistant.quick", 200, null, "{\"names\":\"both original copied identities\"}", "active_users", nowUtc, [first.UserId, second.UserId]);
        var unrelated = AssistantDurableOperation.Create(owner.UserId, Access.Business, Guid.CreateVersion7(), "unrelated", "/mutual/unrelated", Guid.CreateVersion7(),
            "assistant.quick", 200, null, "{\"name\":\"owner identity\"}", "active_users", nowUtc, [owner.UserId]);
        string? originalUnrelated;
        using (var setup = host.Services.CreateScope())
        {
            var provider = setup.ServiceProvider;
            using var tenant = provider.GetRequiredService<ITenantScope>().Enter(tenantId);
            await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                var db = provider.GetRequiredService<ApplicationDbContext>();
                var rows = new[] { firstCopy, secondCopy, thirdCopy, unrelated };
                db.AddRange(rows); db.AddRange(rows.Select(row => AssistantSnapshotRetentionIndex.Create(row.Id, tenantId, nowUtc)));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, ct);
            // Compare the persisted jsonb original, whose whitespace PostgreSQL canonicalizes on insert.
            originalUnrelated = await provider.GetRequiredService<ApplicationDbContext>().Set<AssistantDurableOperation>()
                .AsNoTracking().Where(row => row.Id == unrelated.Id).Select(row => row.SnapshotJson).SingleAsync(ct);
        }
        using var requestedFirst = await AccountJourney.PostAsync(firstClient, "/api/me/deletion", new { reason = "Mutual copies first account" }, ct);
        using var requestedSecond = await AccountJourney.PostAsync(secondClient, "/api/me/deletion", new { reason = "Mutual copies second account" }, ct);
        Assert.Equal(HttpStatusCode.OK, requestedFirst.StatusCode); Assert.Equal(HttpStatusCode.OK, requestedSecond.StatusCode);
        using (var setup = host.Services.CreateScope())
        {
            var provider = setup.ServiceProvider;
            await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async c =>
            {
                var db = provider.GetRequiredService<ApplicationDbContext>();
                var a = await db.Users.SingleAsync(row => row.Id == first.UserId, c);
                var b = await db.Users.SingleAsync(row => row.Id == second.UserId, c);
                db.Entry(a).Property(row => row.DeletionScheduledForUtc).CurrentValue = nowUtc.AddDays(-2);
                db.Entry(b).Property(row => row.DeletionScheduledForUtc).CurrentValue = nowUtc.AddDays(-1);
                return Result.Success();
            }, CommitPolicy.OnSuccess, ct);
        }
        var firstWork = await ClaimAsync(); var secondWork = await ClaimAsync();
        Assert.Equal(first.UserId, firstWork.UserId); Assert.Equal(second.UserId, secondWork.UserId);
        var firstTenant = Assert.Single(firstWork.Tenants, tenant => tenant.TenantId == tenantId);
        var secondTenant = Assert.Single(secondWork.Tenants, tenant => tenant.TenantId == tenantId);
        using var firstScope = host.Services.CreateScope();
        using var secondScope = host.Services.CreateScope();
        var firstTask = firstScope.ServiceProvider.GetRequiredService<IAccountDeletionProcessingService>().ProcessTenantAsync(firstWork, firstTenant, ct);
        var secondTask = secondScope.ServiceProvider.GetRequiredService<IAccountDeletionProcessingService>().ProcessTenantAsync(secondWork, secondTenant, ct);
        try
        {
            var bothReady = Task.WhenAll(rendezvous.FirstReady, rendezvous.SecondReady);
            var firstCompleted = Task.WhenAny(firstTask, secondTask);
            if (await Task.WhenAny(bothReady, firstCompleted) != bothReady)
            {
                var early = await firstCompleted;
                Assert.True((await early).IsSuccess);
                Assert.Fail("A real processor finished before both target identities reached the participant boundary.");
            }
            await bothReady.WaitAsync(TimeSpan.FromSeconds(30), ct);
            using (var probe = host.Services.CreateScope())
            {
                var ids = new[] { first.UserId, second.UserId };
                // FOR SHARE conflicts with the processor's FOR NO KEY UPDATE identity lock.
                var available = await probe.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async c =>
                    Result.Success(await probe.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.SqlQuery<Guid>($"""
                        SELECT "Id" AS "Value" FROM identity."AspNetUsers" WHERE "Id" = ANY({ids}) ORDER BY "Id" FOR SHARE SKIP LOCKED
                        """).ToArrayAsync(c)), CommitPolicy.OnSuccess, ct);
                Assert.True(available.IsSuccess); Assert.Empty(available.Value);
            }
            if (releaseTogether) rendezvous.ReleaseAll();
            else
            {
                rendezvous.Release(releaseSecondFirst ? second.UserId : first.UserId);
                // The other identity remains locked. A foreign share would block this real participant here.
                var completed = await (releaseSecondFirst ? secondTask : firstTask).WaitAsync(TimeSpan.FromSeconds(30), ct);
                Assert.True(completed.IsSuccess);
                Assert.False((releaseSecondFirst ? firstTask : secondTask).IsCompleted);
                rendezvous.ReleaseAll();
            }
            var results = await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(30), ct);
            Assert.All(results, result => Assert.True(result.IsSuccess));
        }
        finally
        {
            rendezvous.ReleaseAll();
            await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(30), ct);
        }
        await FinishAsync(firstWork); await FinishAsync(secondWork);
        using var read = host.Services.CreateScope();
        var services = read.ServiceProvider; var database = services.GetRequiredService<ApplicationDbContext>();
        var statuses = await database.Users.AsNoTracking().Where(row => row.Id == first.UserId || row.Id == second.UserId)
            .Select(row => row.Status).ToArrayAsync(ct);
        Assert.Equal(2, statuses.Length); Assert.All(statuses, status => Assert.Equal(UserStatus.Deleted, status));
        Assert.False(await database.Set<AssistantSnapshotRetentionIndex>()
            .AnyAsync(row => row.OperationId == firstCopy.Id || row.OperationId == secondCopy.Id || row.OperationId == thirdCopy.Id, ct));
        Assert.True(await database.Set<AssistantSnapshotRetentionIndex>().AnyAsync(row => row.OperationId == unrelated.Id, ct));
        using var active = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        Assert.False(await database.Set<AssistantDurableOperation>().AnyAsync(row => row.Id == firstCopy.Id || row.Id == secondCopy.Id, ct));
        var third = await database.Set<AssistantDurableOperation>().AsNoTracking().SingleAsync(row => row.Id == thirdCopy.Id, ct);
        Assert.Null(third.SnapshotJson); Assert.Null(third.QuickKey); Assert.Empty(third.SubjectUserIds);
        Assert.Equal("third", third.BodyHash); Assert.Equal(thirdCopy.Key, third.Key); Assert.Equal("/mutual/third", third.Route);
        var survivor = await database.Set<AssistantDurableOperation>().AsNoTracking().SingleAsync(row => row.Id == unrelated.Id, ct);
        Assert.Equal(originalUnrelated, survivor.SnapshotJson); Assert.Equal(new[] { owner.UserId }, survivor.SubjectUserIds);

        async Task<AccountDeletionWork> ClaimAsync()
        {
            using var scope = host.Services.CreateScope();
            var claimed = await scope.ServiceProvider.GetRequiredService<IAccountDeletionProcessingService>().ClaimNextAsync(ct);
            Assert.True(claimed.IsSuccess); Assert.NotNull(claimed.Value); return claimed.Value;
        }
        async Task FinishAsync(AccountDeletionWork work)
        {
            foreach (var tenant in work.Tenants.Where(tenant => tenant.TenantId != tenantId))
            {
                using var scope = host.Services.CreateScope();
                Assert.True((await scope.ServiceProvider.GetRequiredService<IAccountDeletionProcessingService>().ProcessTenantAsync(work, tenant, ct)).IsSuccess);
            }
            using var complete = host.Services.CreateScope();
            Assert.True((await complete.ServiceProvider.GetRequiredService<IAccountDeletionProcessingService>().CompleteAsync(work, ct)).IsSuccess);
        }
    }

    /// <summary>A test-only neutral participant pauses before production participants; it never replaces their behavior.</summary>
    private sealed class DeletionRendezvous : IAccountDeletionParticipant
    {
        private Guid first; private Guid second; private Guid tenant;
        private readonly TaskCompletionSource firstReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource secondReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource releaseSecond = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task FirstReady => firstReady.Task;
        internal Task SecondReady => secondReady.Task;
        internal void Configure(Guid firstUser, Guid secondUser, Guid tenantId) { first = firstUser; second = secondUser; tenant = tenantId; }
        internal void Release(Guid userId) { if (userId == first) releaseFirst.TrySetResult(); else if (userId == second) releaseSecond.TrySetResult(); }
        internal void ReleaseAll() { releaseFirst.TrySetResult(); releaseSecond.TrySetResult(); }
        public Task<IReadOnlyList<Error>> CheckAsync(AccountDeletionContext context, CancellationToken ct) => Task.FromResult<IReadOnlyList<Error>>([]);
        public Task OnRequestedAsync(AccountDeletionContext context, CancellationToken ct) => Task.CompletedTask;
        public Task OnCancelledAsync(AccountDeletionContext context, CancellationToken ct) => Task.CompletedTask;
        public async Task ExecuteAsync(AccountDeletionContext context, CancellationToken ct)
        {
            if (context.TenantId != tenant) return;
            if (context.UserId == first) { firstReady.TrySetResult(); await releaseFirst.Task.WaitAsync(ct); }
            else if (context.UserId == second) { secondReady.TrySetResult(); await releaseSecond.Task.WaitAsync(ct); }
        }
    }
}
