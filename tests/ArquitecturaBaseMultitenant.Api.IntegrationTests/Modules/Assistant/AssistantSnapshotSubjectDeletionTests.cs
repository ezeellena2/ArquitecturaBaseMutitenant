using System.Net;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantSnapshotSubjectDeletionTests(ApiFactory factory)
{
    [Fact]
    public async Task Real_deletion_purges_whole_original_copies_in_two_other_owners_and_scopes_and_keeps_unrelated_copies()
    {
        var ct = TestContext.Current.CancellationToken;
        using var targetClient = AssistantTestData.Client(factory);
        var target = await AccountJourney.RegisterAsync(factory, targetClient, ct);
        using var firstClient = AssistantTestData.Client(factory);
        var firstOwner = await AccountJourney.RegisterAsync(factory, firstClient, ct);
        using var secondClient = AssistantTestData.Client(factory);
        var secondOwner = await AccountJourney.RegisterAsync(factory, secondClient, ct);
        var firstTenant = await OrganizationJourney.StageAsync(factory, firstOwner, "Snapshot subject first scope", ct);
        var secondTenant = await OrganizationJourney.StageAsync(factory, secondOwner, "Snapshot subject second scope", ct);
        await OrganizationJourney.AddActiveMemberAsync(factory, firstTenant, target.UserId, ct);
        var nowUtc = factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var firstCopy = AssistantDurableOperation.Create(firstOwner.UserId, Access.Business, Guid.CreateVersion7(), "first-hash", "/first-copy", Guid.CreateVersion7(),
            "assistant.quick", 200, null, "{\"original\":\"target name and original result\"}", "active_users", nowUtc, [target.UserId]);
        var secondCopy = AssistantDurableOperation.Create(secondOwner.UserId, Access.Business, Guid.CreateVersion7(), "second-hash", "/second-copy", Guid.CreateVersion7(),
            "assistant.quick", 200, null, "{\"original\":\"target name and original result\"}", "recent_changes", nowUtc, [target.UserId]);
        var unrelated = AssistantDurableOperation.Create(firstOwner.UserId, Access.Business, Guid.CreateVersion7(), "unrelated-hash", "/unrelated", Guid.CreateVersion7(),
            "assistant.quick", 200, null, "{\"original\":\"other account\"}", "active_users", nowUtc, [secondOwner.UserId]);
        var own = AssistantDurableOperation.Create(target.UserId, Access.Business, Guid.CreateVersion7(), "owner-hash", "/owner", Guid.CreateVersion7(),
            "assistant.quick", 200, null, "{}", "active_users", nowUtc);
        var platformCopy = PlatformAssistantDurableOperation.Create(firstOwner.UserId, Guid.CreateVersion7(), "platform-hash", "/platform-copy", Guid.CreateVersion7(),
            "assistant.quick", 200, null, "{\"original\":\"target name and original result\"}", "today_security", nowUtc, [target.UserId]);
        await StageAsync(firstTenant, firstCopy, unrelated, own);
        await StageAsync(secondTenant, secondCopy);
        string? originalUnrelated;
        using (var originalRead = factory.Services.CreateScope())
        {
            var provider = originalRead.ServiceProvider;
            using var tenant = provider.GetRequiredService<ITenantScope>().Enter(firstTenant);
            originalUnrelated = await provider.GetRequiredService<ApplicationDbContext>().Set<AssistantDurableOperation>()
                .AsNoTracking().Where(row => row.Id == unrelated.Id).Select(row => row.SnapshotJson).SingleAsync(ct);
        }
        using (var setup = factory.Services.CreateScope())
        {
            var provider = setup.ServiceProvider;
            await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                provider.GetRequiredService<ApplicationDbContext>().AddRange(platformCopy,
                    AssistantSnapshotRetentionIndex.Create(platformCopy.Id, null, nowUtc));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, ct);
        }
        using var requested = await AccountJourney.PostAsync(targetClient, "/api/me/deletion", new { reason = "Baja de referencias del Asistente" }, ct);
        Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
        using (var setup = factory.Services.CreateScope())
        {
            var provider = setup.ServiceProvider;
            await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async c =>
            {
                var db = provider.GetRequiredService<ApplicationDbContext>();
                var user = await db.Users.SingleAsync(row => row.Id == target.UserId, c);
                db.Entry(user).Property(row => row.DeletionScheduledForUtc).CurrentValue = nowUtc.AddDays(-1);
                return Result.Success();
            }, CommitPolicy.OnSuccess, ct);
        }
        var worker = ActivatorUtilities.CreateInstance<AccountDeletionWorker>(factory.Services);
        await worker.RunOnceAsync(ct); await worker.RunOnceAsync(ct);
        using var read = factory.Services.CreateScope();
        var services = read.ServiceProvider; var database = services.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(UserStatus.Deleted, (await database.Users.AsNoTracking().SingleAsync(row => row.Id == target.UserId, ct)).Status);
        var platform = await database.Set<PlatformAssistantDurableOperation>().AsNoTracking().SingleAsync(row => row.Id == platformCopy.Id, ct);
        Assert.Null(platform.SnapshotJson); Assert.Null(platform.QuickKey); Assert.Empty(platform.SubjectUserIds);
        Assert.Equal("platform-hash", platform.BodyHash); Assert.Equal("/platform-copy", platform.Route);
        Assert.False(await database.Set<AssistantSnapshotRetentionIndex>()
            .AnyAsync(row => row.OperationId == firstCopy.Id || row.OperationId == secondCopy.Id || row.OperationId == own.Id || row.OperationId == platformCopy.Id, ct));
        Assert.True(await database.Set<AssistantSnapshotRetentionIndex>().AnyAsync(row => row.OperationId == unrelated.Id, ct));
        using (services.GetRequiredService<ITenantScope>().Enter(firstTenant))
        {
            var copy = await database.Set<AssistantDurableOperation>().AsNoTracking().SingleAsync(row => row.Id == firstCopy.Id, ct);
            Assert.Null(copy.SnapshotJson); Assert.Null(copy.QuickKey); Assert.Empty(copy.SubjectUserIds);
            Assert.Equal("first-hash", copy.BodyHash); Assert.Equal(firstCopy.Key, copy.Key);
            Assert.False(await database.Set<AssistantDurableOperation>().AnyAsync(row => row.Id == own.Id, ct));
            Assert.Equal(originalUnrelated, (await database.Set<AssistantDurableOperation>().AsNoTracking().SingleAsync(row => row.Id == unrelated.Id, ct)).SnapshotJson);
        }
        using (services.GetRequiredService<ITenantScope>().Enter(secondTenant))
        {
            var copy = await database.Set<AssistantDurableOperation>().AsNoTracking().SingleAsync(row => row.Id == secondCopy.Id, ct);
            Assert.Null(copy.SnapshotJson); Assert.Null(copy.QuickKey); Assert.Empty(copy.SubjectUserIds);
            Assert.Equal("second-hash", copy.BodyHash); Assert.Equal(secondCopy.Key, copy.Key);
        }

        async Task StageAsync(Guid tenantId, params AssistantDurableOperation[] rows)
        {
            using var scope = factory.Services.CreateScope();
            var provider = scope.ServiceProvider;
            using var tenant = provider.GetRequiredService<ITenantScope>().Enter(tenantId);
            await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                var db = provider.GetRequiredService<ApplicationDbContext>();
                db.AddRange(rows);
                db.AddRange(rows.Select(row => AssistantSnapshotRetentionIndex.Create(row.Id, tenantId, nowUtc)));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, ct);
        }
    }
}
