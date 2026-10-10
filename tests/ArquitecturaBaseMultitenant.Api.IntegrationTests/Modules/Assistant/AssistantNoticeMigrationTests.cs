using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantNoticeMigrationTests
{
    [Fact]
    public async Task Upgrade_consolidates_historical_access_duplicates_and_down_keeps_the_original_first_evidence()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var db = new ApplicationDbContext(options.Options, new NoTenant());
        var migrations = db.Database.GetMigrations().ToArray();
        var noticeMigration = Assert.Single(migrations, migration => migration.EndsWith("_AssistantNoticeUserVersion", StringComparison.Ordinal));
        var noticeIndex = Array.IndexOf(migrations, noticeMigration);
        Assert.True(noticeIndex > 0);
        var predecessor = migrations[noticeIndex - 1];
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(predecessor, ct);
        var owner = Guid.NewGuid();
        var secondOwner = Guid.NewGuid();
        var acceptedAtUtc = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var first = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var later = Guid.Parse("00000000-0000-0000-0000-000000000002");
        await InsertAsync(first, owner, Access.Consumer, "1", acceptedAtUtc);
        await InsertAsync(later, owner, Access.Business, "1", acceptedAtUtc);
        var earlierBusiness = Guid.NewGuid();
        await InsertAsync(earlierBusiness, secondOwner, Access.Business, "1", acceptedAtUtc);
        await InsertAsync(Guid.NewGuid(), secondOwner, Access.Consumer, "1", acceptedAtUtc.AddHours(1));
        await InsertAsync(Guid.NewGuid(), owner, Access.Business, "2", acceptedAtUtc.AddDays(1));
        await migrator.MigrateAsync(noticeMigration, ct);
        var rows = await db.Set<AssistantNoticeAcceptance>().AsNoTracking().ToArrayAsync(ct);
        Assert.Equal(3, rows.Length);
        var kept = Assert.Single(rows, row => row.OwnerUserId == owner && row.NoticeVersion == "1");
        Assert.Equal(first, kept.Id);
        Assert.Equal(Access.Consumer, kept.Access);
        Assert.Equal(acceptedAtUtc, kept.AcceptedAtUtc);
        Assert.Equal(earlierBusiness, Assert.Single(rows, row => row.OwnerUserId == secondOwner).Id);
        var conflict = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(Guid.NewGuid(), owner, Access.Business, "1", acceptedAtUtc));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, conflict.SqlState);
        await migrator.MigrateAsync(predecessor, ct);
        Assert.Equal(3, await db.Set<AssistantNoticeAcceptance>().CountAsync(ct));
        await InsertAsync(Guid.NewGuid(), owner, Access.Business, "1", acceptedAtUtc.AddHours(2));
        await migrator.MigrateAsync(noticeMigration, ct);
        Assert.Equal(first, (await db.Set<AssistantNoticeAcceptance>().AsNoTracking()
            .SingleAsync(row => row.OwnerUserId == owner && row.NoticeVersion == "1", ct)).Id);

        Task InsertAsync(Guid id, Guid user, Access access, string version, DateTime timestampUtc) =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO platform."AssistantNoticeAcceptances" ("Id", "OwnerUserId", "Access", "NoticeVersion", "AcceptedAtUtc")
                VALUES ({id}, {user}, {access.ToString()}, {version}, {timestampUtc})
                """, ct);
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No tenant.");
    }
}
