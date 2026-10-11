using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Retention;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantSnapshotRetentionTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Exact_expiry_purges_content_and_index_atomically_with_retry_and_keeps_binding(bool platform)
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenantId = await AssistantTestData.ActiveTenantAsync(client);
        var createdAtUtc = new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc);
        var scopeId = platform ? (Guid?)null : tenantId;
        var subject = Guid.CreateVersion7(); var key = Guid.CreateVersion7(); var conversation = Guid.CreateVersion7();
        Guid operationId;
        using (var setup = factory.Services.CreateScope())
        {
            var provider = setup.ServiceProvider;
            using var tenant = scopeId is { } id ? provider.GetRequiredService<ITenantScope>().Enter(id) : null;
            var row = await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
                Task.FromResult(Result.Success(provider.GetRequiredService<IAssistantDurableRepository>().Add(account.UserId,
                    platform ? Access.Platform : Access.Consumer, new IdempotentRequest(key, "hash", "/retention/fixture"), conversation,
                    new IdempotentReference("assistant.quick", Guid.Empty, 200, null), "{}", "fixture", createdAtUtc, [subject]))),
                CommitPolicy.OnSuccess, Ct);
            Assert.True(row.IsSuccess); operationId = row.Value.Id;
        }
        var candidate = new AssistantSnapshotRetentionCandidate(operationId, scopeId);
        Assert.Equal(0, await PurgeAsync(candidate, createdAtUtc.AddHours(24).AddTicks(-1), true));
        Assert.Equal(1, await PurgeAsync(candidate, createdAtUtc.AddHours(24), false));
        await AssertBindingAsync(true);
        Assert.Equal(1, await PurgeAsync(candidate, createdAtUtc.AddHours(24), true));
        Assert.Equal(0, await PurgeAsync(candidate, createdAtUtc.AddDays(90), true));
        await AssertBindingAsync(false);

        async Task AssertBindingAsync(bool content)
        {
            using var read = factory.Services.CreateScope();
            var provider = read.ServiceProvider;
            using var tenant = scopeId is { } id ? provider.GetRequiredService<ITenantScope>().Enter(id) : null;
            var row = await provider.GetRequiredService<IAssistantDurableRepository>()
                .GetByKeyAsync(account.UserId, platform ? Access.Platform : Access.Consumer, key, Ct);
            Assert.NotNull(row); Assert.Equal(conversation, row.ConversationId); Assert.Equal("hash", row.BodyHash);
            Assert.Equal("/retention/fixture", row.Route); Assert.Equal(createdAtUtc, row.CreatedAtUtc);
            Assert.Equal(content, row.SnapshotJson is not null); Assert.Equal(content, row.QuickKey is not null);
            Assert.Equal(content ? new[] { subject } : [], row.SubjectUserIds);
            Assert.Equal(content, await provider.GetRequiredService<ApplicationDbContext>().Set<AssistantSnapshotRetentionIndex>()
                .AnyAsync(value => value.OperationId == operationId, Ct));
        }
    }

    [Fact]
    public async Task Installed_worker_cleans_private_and_platform_snapshots_with_feature_gates_off_and_preserves_saved_conversation()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenantId = await AssistantTestData.ActiveTenantAsync(client);
        await AssistantTestData.PersonalAsync(factory, false);
        var nowUtc = factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var saved = AssistantConversation.Create(account.UserId, Access.Consumer, nowUtc.AddDays(-90));
        saved.Update(null, true, nowUtc);
        var privateBinding = AssistantDurableOperation.Create(account.UserId, Access.Consumer, Guid.CreateVersion7(), "hash", "/private", saved.Id,
            "assistant.quick", 200, null, "{}", "fixture", nowUtc.AddHours(-25));
        var platformBinding = PlatformAssistantDurableOperation.Create(account.UserId, Guid.CreateVersion7(), "hash", "/platform", Guid.CreateVersion7(),
            "assistant.quick", 200, null, "{}", "fixture", nowUtc.AddHours(-25));
        using (var setup = factory.Services.CreateScope())
        {
            var provider = setup.ServiceProvider;
            using var tenant = provider.GetRequiredService<ITenantScope>().Enter(tenantId);
            await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                provider.GetRequiredService<ApplicationDbContext>().AddRange(saved, privateBinding, platformBinding,
                    AssistantSnapshotRetentionIndex.Create(privateBinding.Id, tenantId, privateBinding.CreatedAtUtc),
                    AssistantSnapshotRetentionIndex.Create(platformBinding.Id, null, platformBinding.CreatedAtUtc));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }
        var worker = ActivatorUtilities.CreateInstance<AssistantSnapshotRetentionWorker>(factory.Services);
        await worker.RunOnceAsync(Ct); await worker.RunOnceAsync(Ct);
        using var read = factory.Services.CreateScope();
        var services = read.ServiceProvider; var db = services.GetRequiredService<ApplicationDbContext>();
        Assert.Null((await db.Set<PlatformAssistantDurableOperation>().AsNoTracking().SingleAsync(row => row.Id == platformBinding.Id, Ct)).SnapshotJson);
        using var active = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        Assert.Null((await db.Set<AssistantDurableOperation>().AsNoTracking().SingleAsync(row => row.Id == privateBinding.Id, Ct)).SnapshotJson);
        Assert.True((await db.Set<AssistantConversation>().AsNoTracking().SingleAsync(row => row.Id == saved.Id, Ct)).IsSaved);
        Assert.False(await db.Set<AssistantSnapshotRetentionIndex>().AnyAsync(row => row.OperationId == privateBinding.Id || row.OperationId == platformBinding.Id, Ct));
    }

    private async Task<int> PurgeAsync(AssistantSnapshotRetentionCandidate candidate, DateTime nowUtc, bool commit)
    {
        using var scope = factory.Services.CreateScope();
        var provider = scope.ServiceProvider;
        using var tenant = candidate.ScopeTenantId is { } id ? provider.GetRequiredService<ITenantScope>().Enter(id) : null;
        var count = 0;
        var result = await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            count = await provider.GetRequiredService<IAssistantSnapshotRetentionRepository>().PurgeExpiredAsync(candidate, nowUtc, ct);
            return commit ? Result.Success() : (Result)UserErrors.NotFound;
        }, CommitPolicy.OnSuccess, Ct);
        Assert.Equal(commit, result.IsSuccess);
        return count;
    }

    [Fact]
    public async Task Two_workers_skip_a_locked_index_then_retry_after_rollback_without_losing_the_original_copy()
    {
        var nowUtc = factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var binding = PlatformAssistantDurableOperation.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "hash", "/locked", Guid.CreateVersion7(),
            "assistant.quick", 200, null, "{}", "fixture", nowUtc.AddHours(-25), [Guid.CreateVersion7()]);
        using (var setup = factory.Services.CreateScope())
        {
            var provider = setup.ServiceProvider;
            await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                provider.GetRequiredService<ApplicationDbContext>().AddRange(binding,
                    AssistantSnapshotRetentionIndex.Create(binding.Id, null, binding.CreatedAtUtc));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var holder = factory.Services.CreateScope();
        var holding = holder.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var db = holder.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Single(await db.Database.SqlQuery<Guid>($"""
                SELECT "Id" AS "Value" FROM platform."AssistantSnapshotRetentionIndices" WHERE "OperationId" = {binding.Id} FOR UPDATE
                """).ToArrayAsync(ct));
            locked.TrySetResult();
            await release.Task.WaitAsync(ct);
            return (Result)UserErrors.NotFound;
        }, CommitPolicy.OnSuccess, Ct);
        try
        {
            if (await Task.WhenAny(locked.Task, holding) == holding) await holding;
            await locked.Task.WaitAsync(Ct);
            var first = ActivatorUtilities.CreateInstance<AssistantSnapshotRetentionWorker>(factory.Services);
            var second = ActivatorUtilities.CreateInstance<AssistantSnapshotRetentionWorker>(factory.Services);
            await Task.WhenAll(first.RunOnceAsync(Ct), second.RunOnceAsync(Ct));
            using var read = factory.Services.CreateScope();
            var db = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal("{}", (await db.Set<PlatformAssistantDurableOperation>().AsNoTracking().SingleAsync(row => row.Id == binding.Id, Ct)).SnapshotJson);
            Assert.True(await db.Set<AssistantSnapshotRetentionIndex>().AnyAsync(row => row.OperationId == binding.Id, Ct));
        }
        finally { release.TrySetResult(); await holding; }
        var retry = ActivatorUtilities.CreateInstance<AssistantSnapshotRetentionWorker>(factory.Services);
        await retry.RunOnceAsync(Ct);
        using var final = factory.Services.CreateScope();
        var finalDb = final.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var purged = await finalDb.Set<PlatformAssistantDurableOperation>().AsNoTracking().SingleAsync(row => row.Id == binding.Id, Ct);
        Assert.Null(purged.SnapshotJson); Assert.Null(purged.QuickKey); Assert.Empty(purged.SubjectUserIds);
        Assert.False(await finalDb.Set<AssistantSnapshotRetentionIndex>().AnyAsync(row => row.OperationId == binding.Id, Ct));
    }

    [Fact]
    public async Task Worker_drains_more_than_one_hundred_and_a_fully_locked_first_page_does_not_starve_later_scopes()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        var ct = deadline.Token;
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var client = AssistantTestData.Client(isolated);
        var account = await AccountJourney.RegisterAsync(isolated, client, ct);
        var tenantId = await AssistantTestData.ActiveTenantAsync(client);
        var nowUtc = isolated.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var lockedRows = Enumerable.Range(0, 100).Select(_ => PlatformAssistantDurableOperation.Create(account.UserId, Guid.CreateVersion7(),
            "locked-hash", "/locked-backlog", Guid.CreateVersion7(), "assistant.quick", 200, null, "{}", "fixture", nowUtc.AddHours(-26), [account.UserId])).ToArray();
        var freeRows = Enumerable.Range(0, 103).Select(_ => AssistantDurableOperation.Create(account.UserId, Access.Consumer, Guid.CreateVersion7(),
            "free-hash", "/free-backlog", Guid.CreateVersion7(), "assistant.quick", 200, null, "{}", "fixture", nowUtc.AddHours(-25), [account.UserId])).ToArray();
        using (var setup = isolated.Services.CreateScope())
        {
            var provider = setup.ServiceProvider;
            using var tenant = provider.GetRequiredService<ITenantScope>().Enter(tenantId);
            await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                var db = provider.GetRequiredService<ApplicationDbContext>();
                db.AddRange(lockedRows); db.AddRange(freeRows);
                db.AddRange(lockedRows.Select(row => AssistantSnapshotRetentionIndex.Create(row.Id, null, row.CreatedAtUtc)));
                db.AddRange(freeRows.Select(row => AssistantSnapshotRetentionIndex.Create(row.Id, tenantId, row.CreatedAtUtc)));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, ct);
        }
        var lockedIds = lockedRows.Select(row => row.Id).ToArray();
        using (var read = isolated.Services.CreateScope())
        {
            var repository = read.ServiceProvider.GetRequiredService<IAssistantSnapshotRetentionRepository>();
            var first = await repository.ListExpiredAsync(nowUtc, ct);
            Assert.Equal(100, first.Count); Assert.All(first, candidate => Assert.Contains(candidate.OperationId, lockedIds));
            var second = await repository.ListExpiredAsync(nowUtc, ct, first[^1]);
            Assert.Equal(100, second.Count); Assert.All(second, candidate => Assert.Equal(tenantId, candidate.ScopeTenantId));
            var third = await repository.ListExpiredAsync(nowUtc, ct, second[^1]);
            Assert.Equal(3, third.Count); Assert.Empty(await repository.ListExpiredAsync(nowUtc, ct, third[^1]));
            Assert.Equal(203, first.Concat(second).Concat(third).Select(candidate => candidate.OperationId).Distinct().Count());
        }
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var holder = isolated.Services.CreateScope();
        var holding = holder.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async c =>
        {
            var db = holder.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(100, (await db.Database.SqlQuery<Guid>($"""
                SELECT "Id" AS "Value" FROM platform."AssistantSnapshotRetentionIndices"
                WHERE "OperationId" = ANY({lockedIds}) ORDER BY "OperationId" FOR UPDATE
                """).ToArrayAsync(c)).Length);
            locked.TrySetResult(); await release.Task.WaitAsync(c);
            return (Result)UserErrors.NotFound;
        }, CommitPolicy.OnSuccess, ct);
        try
        {
            if (await Task.WhenAny(locked.Task, holding) == holding) await holding;
            await locked.Task.WaitAsync(ct);
            var worker = ActivatorUtilities.CreateInstance<AssistantSnapshotRetentionWorker>(isolated.Services);
            Assert.Equal(103, await worker.RunOnceAsync(ct));
            using var read = isolated.Services.CreateScope();
            var provider = read.ServiceProvider; var db = provider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(100, await db.Set<AssistantSnapshotRetentionIndex>().CountAsync(ct));
            Assert.All(await db.Set<PlatformAssistantDurableOperation>().AsNoTracking().ToArrayAsync(ct), row =>
            {
                Assert.Equal("{}", row.SnapshotJson); Assert.Equal(new[] { account.UserId }, row.SubjectUserIds);
            });
            using var tenant = provider.GetRequiredService<ITenantScope>().Enter(tenantId);
            var purged = await db.Set<AssistantDurableOperation>().AsNoTracking().ToArrayAsync(ct);
            Assert.Equal(103, purged.Length);
            Assert.All(purged, row =>
            {
                Assert.Null(row.SnapshotJson); Assert.Null(row.QuickKey); Assert.Empty(row.SubjectUserIds);
                Assert.Equal("free-hash", row.BodyHash); Assert.Equal("/free-backlog", row.Route);
            });
        }
        finally { release.TrySetResult(); Assert.True((await holding).IsFailure); }
        var retry = ActivatorUtilities.CreateInstance<AssistantSnapshotRetentionWorker>(isolated.Services);
        Assert.Equal(100, await retry.RunOnceAsync(ct));
        Assert.Equal(0, await retry.RunOnceAsync(ct));
        using var final = isolated.Services.CreateScope();
        var database = final.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await database.Set<AssistantSnapshotRetentionIndex>().ToArrayAsync(ct));
        var finalRows = await database.Set<PlatformAssistantDurableOperation>().AsNoTracking().ToArrayAsync(ct);
        Assert.Equal(100, finalRows.Length);
        Assert.All(finalRows, row => { Assert.Null(row.SnapshotJson); Assert.Null(row.QuickKey); Assert.Empty(row.SubjectUserIds); });
    }
}
