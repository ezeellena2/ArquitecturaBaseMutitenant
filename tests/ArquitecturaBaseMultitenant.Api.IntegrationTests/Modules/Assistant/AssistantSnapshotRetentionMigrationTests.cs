using System.Net;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

public sealed class AssistantSnapshotRetentionMigrationTests
{
    [Fact]
    public async Task Upgrade_freezes_previous_xmin_preserves_original_private_HTTP_and_platform_snapshot_and_purges_missing_subject()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var client = AssistantTestData.Client(isolated);
        var account = await AccountJourney.RegisterAsync(isolated, client, ct);
        using (var setup = isolated.Services.CreateScope())
        {
            var provider = setup.ServiceProvider;
            await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async token =>
            {
                await provider.GetRequiredService<IUserRepository>().FillMissingProfileNamesAsync(account.UserId, "Legacy", "Owner", token);
                return Result.Success();
            }, CommitPolicy.OnSuccess, ct);
        }
        var tenantId = await AssistantTestData.ActiveTenantAsync(client);
        await AssistantTestData.PersonalAsync(isolated, true);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client);
        var key = Guid.CreateVersion7();
        using var quick = await AssistantDurableConversationTests.PostAsync(client, "/api/me/assistant/quick/my_signin", "{}", key);
        Assert.Equal(HttpStatusCode.OK, quick.StatusCode);
        var originalHttp = await quick.Content.ReadAsStringAsync(ct);
        Assert.Contains("Legacy Owner", originalHttp, StringComparison.Ordinal);
        var nowUtc = isolated.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var platformKey = Guid.CreateVersion7(); var platformConversationId = Guid.CreateVersion7();
        var snapshot = AssistantSnapshotBuilder.Serialize(new AssistantDurableSnapshot(
            new AssistantConversationSummaryResponse(platformConversationId, null, false, 0, nowUtc, nowUtc), [], null));
        var platform = PlatformAssistantDurableOperation.Create(account.UserId, platformKey, "hash", "/platform/create", platformConversationId,
            "assistant.conversation", 201, null, snapshot, null, nowUtc);
        var missingSubject = Guid.CreateVersion7(); var unavailableKey = Guid.CreateVersion7();
        var unavailableSnapshot = """
            {"Conversation":{"Id":"00000000-0000-0000-0000-000000000001","Title":null,"IsSaved":false,"Version":0,"CreatedAtUtc":"2026-10-11T00:00:00Z","LastActivityAtUtc":"2026-10-11T00:00:00Z"},"Messages":[],"Result":{"Text":"names","Links":[],"Data":{"$type":"recoveries","Recoveries":[{"Id":"00000000-0000-0000-0000-000000000002","DisplayName":"Copied old account","Status":"Pending","CreatedAtUtc":"2026-10-11T00:00:00Z"}],"TotalCount":1,"HasMore":false}}}
            """;
        var unavailable = PlatformAssistantDurableOperation.Create(account.UserId, unavailableKey, "hash", "/platform/quick", Guid.CreateVersion7(),
            "assistant.quick", 200, null, unavailableSnapshot, "pending_recoveries", nowUtc, [missingSubject]);
        using (var setup = isolated.Services.CreateScope())
        {
            var provider = setup.ServiceProvider;
            await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                provider.GetRequiredService<ApplicationDbContext>().AddRange(platform, unavailable,
                    AssistantSnapshotRetentionIndex.Create(platform.Id, null, nowUtc), AssistantSnapshotRetentionIndex.Create(unavailable.Id, null, nowUtc));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, ct);
        }
        var originalPlatform = AssistantSnapshotBuilder.Serialize(AssistantSnapshotBuilder.Restore(snapshot, platform.Version));
        uint privateVersion;
        Guid privateId;
        string privateJson;
        using (var read = isolated.Services.CreateScope())
        {
            var provider = read.ServiceProvider;
            using var tenant = provider.GetRequiredService<ITenantScope>().Enter(tenantId);
            var row = await provider.GetRequiredService<ApplicationDbContext>().Set<AssistantDurableOperation>().AsNoTracking()
                .SingleAsync(value => value.OwnerUserId == account.UserId && value.Key == key, ct);
            Assert.Null(row.OriginalSnapshotVersion);
            privateVersion = row.Version; privateId = row.Id; privateJson = row.SnapshotJson!;
        }
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var migrationDb = new ApplicationDbContext(options.Options, new NoTenant());
        var migrator = migrationDb.GetService<IMigrator>();
        await migrator.MigrateAsync("20261010223305_AssistantEditableModelCatalog", ct);
        await migrator.MigrateAsync("20261011141438_AssistantSnapshotRetention", ct);
        Assert.False(migrationDb.Database.HasPendingModelChanges());
        using (var read = isolated.Services.CreateScope())
        {
            var provider = read.ServiceProvider; var db = provider.GetRequiredService<ApplicationDbContext>();
            var durable = provider.GetRequiredService<IAssistantDurableRepository>();
            var platformRow = await db.Set<PlatformAssistantDurableOperation>().AsNoTracking().SingleAsync(row => row.Id == platform.Id, ct);
            Assert.Equal(platform.Version, platformRow.OriginalSnapshotVersion); Assert.NotEqual(platform.Version, platformRow.Version);
            var byPlatformKey = await durable.GetByKeyAsync(account.UserId, Access.Platform, platformKey, ct);
            var byPlatformId = await durable.GetByIdAsync(account.UserId, Access.Platform, platform.Id, ct);
            Assert.Equal(platform.Version, byPlatformKey!.Version); Assert.Equal(platform.Version, byPlatformId!.Version);
            Assert.Equal(originalPlatform, AssistantSnapshotBuilder.Serialize(AssistantSnapshotBuilder.Restore(platformRow.SnapshotJson!, byPlatformId.Version)));
            var purged = await db.Set<PlatformAssistantDurableOperation>().AsNoTracking().SingleAsync(row => row.Id == unavailable.Id, ct);
            Assert.Null(purged.SnapshotJson); Assert.Null(purged.QuickKey); Assert.Empty(purged.SubjectUserIds);
            Assert.Equal("hash", purged.BodyHash); Assert.Equal(unavailableKey, purged.Key);
            Assert.False(await db.Set<AssistantSnapshotRetentionIndex>().AnyAsync(row => row.OperationId == unavailable.Id, ct));
            using var tenant = provider.GetRequiredService<ITenantScope>().Enter(tenantId);
            var privateRow = await db.Set<AssistantDurableOperation>().AsNoTracking().SingleAsync(row => row.Id == privateId, ct);
            Assert.Equal(privateVersion, privateRow.OriginalSnapshotVersion); Assert.NotEqual(privateVersion, privateRow.Version);
            Assert.Equal(privateJson, privateRow.SnapshotJson);
            Assert.Equal(new[] { account.UserId }, privateRow.SubjectUserIds);
            var byKey = await durable.GetByKeyAsync(account.UserId, Access.Consumer, key, ct);
            var byId = await durable.GetByIdAsync(account.UserId, Access.Consumer, privateId, ct);
            Assert.Equal(privateVersion, byKey!.Version); Assert.Equal(privateVersion, byId!.Version);
        }
        using var replay = await AssistantDurableConversationTests.PostAsync(client, "/api/me/assistant/quick/my_signin", "{}", key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode); Assert.Equal(originalHttp, await replay.Content.ReadAsStringAsync(ct));
        using var mismatch = await AssistantDurableConversationTests.PostAsync(client, "/api/me/assistant/quick/my_organizations", "{}", key);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, mismatch.StatusCode);
        using (var mutate = isolated.Services.CreateScope())
        {
            var provider = mutate.ServiceProvider;
            using var tenant = provider.GetRequiredService<ITenantScope>().Enter(tenantId);
            await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async c =>
            {
                var db = provider.GetRequiredService<ApplicationDbContext>();
                var row = await db.Set<AssistantDurableOperation>().SingleAsync(value => value.Id == privateId, c);
                row.PreserveSnapshotVersion();
                db.Entry(row).Property(value => value.OriginalSnapshotVersion).IsModified = true;
                return Result.Success();
            }, CommitPolicy.OnSuccess, ct);
        }
        using var afterMetadataUpdate = await AssistantDurableConversationTests.PostAsync(client, "/api/me/assistant/quick/my_signin", "{}", key);
        Assert.Equal(HttpStatusCode.OK, afterMetadataUpdate.StatusCode);
        Assert.Equal(originalHttp, await afterMetadataUpdate.Content.ReadAsStringAsync(ct));
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No tenant.");
    }
}
