using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

public sealed class AccountRecoveryNoticeMigrationTests
{
    [Fact]
    public async Task Empty_and_upgraded_up_down_up_preserves_notices_pending_requests_grants_and_cascade()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var db = new ApplicationDbContext(options.Options, new TenantContext());
        var migrator = db.GetService<IMigrator>();
        var migrations = db.Database.GetMigrations().ToArray();
        var previous = migrations[^2];
        var current = migrations[^1];
        Assert.EndsWith("_AccountRecoveryNoticeReceipt", current, StringComparison.Ordinal);
        await migrator.MigrateAsync(previous, ct);
        await migrator.MigrateAsync(current, ct);
        Assert.Empty(await db.AccountRecoveryNotices.AsNoTracking().ToArrayAsync(ct));
        await migrator.MigrateAsync(previous, ct);
        var sentId = Guid.CreateVersion7();
        var pendingId = Guid.CreateVersion7();
        var nowUtc = new DateTime(2026, 10, 10, 15, 0, 0, DateTimeKind.Utc);
        var resolvedAtUtc = nowUtc.AddMinutes(1);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO platform."AccountRecoveryRequests"
                ("Id", "LoginCodeId", "PreviousMethodType", "PreviousMethodValue", "NewMethodType", "NewMethodValue",
                 "Status", "CreatedAtUtc", "AccountNotifiedAtUtc")
            VALUES ({sentId}, {Guid.CreateVersion7()}, 'Email', 'before@example.test', 'Email', 'sent@example.test',
                    'Pending', {nowUtc}, {resolvedAtUtc}),
                   ({pendingId}, {Guid.CreateVersion7()}, 'Email', 'before@example.test', 'Email', 'pending@example.test',
                    'Pending', {nowUtc}, NULL);
            """, ct);
        var originalVersions = await VersionsAsync();
        await migrator.MigrateAsync(current, ct);
        Assert.Equal(originalVersions, await VersionsAsync());
        await AssertUpgradedAsync();
        await migrator.MigrateAsync(previous, ct);
        await using (var connection = new NpgsqlConnection(isolated.RuntimeConnectionString))
        {
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand("""
                SELECT "Id", "AccountNotifiedAtUtc" FROM platform."AccountRecoveryRequests" ORDER BY "AccountNotifiedAtUtc" NULLS LAST;
                """, connection);
            await using var reader = await command.ExecuteReaderAsync(ct);
            Assert.True(await reader.ReadAsync(ct));
            Assert.Equal(sentId, reader.GetGuid(0));
            Assert.Equal(resolvedAtUtc, reader.GetDateTime(1));
            Assert.True(await reader.ReadAsync(ct));
            Assert.Equal(pendingId, reader.GetGuid(0));
            Assert.True(reader.IsDBNull(1));
            Assert.False(await reader.ReadAsync(ct));
        }
        var downgradedVersions = await VersionsAsync();
        await migrator.MigrateAsync(current, ct);
        Assert.Equal(downgradedVersions, await VersionsAsync());
        await AssertUpgradedAsync();
        Assert.False(db.Database.HasPendingModelChanges());

        using var client = isolated.CreateClient();
        await using (var scope = isolated.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async work =>
            {
                var repository = services.GetRequiredService<IAccountRecoveryRepository>();
                Assert.Equal(pendingId, Assert.Single(await repository.ListAwaitingAccountNoticeAsync(50, work)).Id);
                repository.AddAccountNotice(AccountRecoveryNotice.Complete(pendingId, nowUtc.AddMinutes(2)));
                return Result.Success();
            }, CommitPolicy.OnSuccess, ct);
        }
        Assert.Equal(2, await db.AccountRecoveryNotices.CountAsync(ct));
        // La eliminación efectiva de datos personales borra los pedidos; el receipt no puede quedar huérfano.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM platform."AccountRecoveryRequests" WHERE "Id" IN ({sentId}, {pendingId});
            """, ct);
        Assert.Empty(await db.AccountRecoveryNotices.AsNoTracking().ToArrayAsync(ct));

        async Task AssertUpgradedAsync()
        {
            var notice = Assert.Single(await db.AccountRecoveryNotices.AsNoTracking().ToArrayAsync(ct));
            Assert.Equal(sentId, notice.Id);
            Assert.Equal(resolvedAtUtc, notice.ResolvedAtUtc);
            Assert.Equal(2, await db.AccountRecoveryRequests.CountAsync(ct));
            await using var connection = new NpgsqlConnection(isolated.RuntimeConnectionString);
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand("""
                SELECT relrowsecurity, relforcerowsecurity,
                    has_table_privilege(current_user, oid, 'SELECT') AND has_table_privilege(current_user, oid, 'INSERT')
                    AND has_table_privilege(current_user, oid, 'UPDATE') AND has_table_privilege(current_user, oid, 'DELETE')
                FROM pg_class WHERE oid='platform."AccountRecoveryNotices"'::regclass;
                """, connection);
            await using var reader = await command.ExecuteReaderAsync(ct);
            Assert.True(await reader.ReadAsync(ct));
            Assert.False(reader.GetBoolean(0));
            Assert.False(reader.GetBoolean(1));
            Assert.True(reader.GetBoolean(2));
        }

        Task<uint[]> VersionsAsync() => db.AccountRecoveryRequests.AsNoTracking().OrderBy(request => request.Id)
            .Select(request => request.Version).ToArrayAsync(ct);
    }
}
