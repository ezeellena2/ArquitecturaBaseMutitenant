using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Identity;

/// <summary>
/// La migración de la campana (base de la 12): el Up crea lo que abrió cada cuenta, que la aplicación lee, agrega y borra
/// pero nunca edita; el Down sin nada leído vuelve atrás y, con algo leído, se niega antes que borrarlo.
/// </summary>
public sealed class NotificationReadsMigrationTests
{
    [Fact]
    public async Task Down_goes_back_without_reads_and_refuses_to_lose_one()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var client = isolated.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(isolated, client, ct);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var db = new ApplicationDbContext(options.Options, new NoTenant());
        var migrator = db.GetService<IMigrator>();
        const string previous = "20261107202621_SortAndTenantRevocationIndexes";
        const string current = "20261108234332_NotificationReads";

        await migrator.MigrateAsync(previous, ct);
        Assert.False(await TableExistsAsync());

        await migrator.MigrateAsync(current, ct);
        Assert.True(await TableExistsAsync());
        var privileges = await RuntimePrivilegesAsync();
        Assert.Equal([true, true, true, false], privileges);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO identity."NotificationReads" ("Id", "UserId", "NotificationId", "ReadAtUtc")
            VALUES ({Guid.CreateVersion7()}, {account.UserId}, 'personalLoginMethod:leido',
                {new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc)})
            """, ct);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(previous, ct));
        Assert.Equal(PostgresErrorCodes.RaiseException, refused.SqlState);
        Assert.True(await TableExistsAsync());
        Assert.Equal(1, await db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value" FROM identity."NotificationReads" WHERE "UserId" = {account.UserId}
            """).SingleAsync(ct));
        Assert.Contains(current, await db.Database.GetAppliedMigrationsAsync(ct));
        Assert.False(db.Database.HasPendingModelChanges());

        async Task<bool> TableExistsAsync()
        {
            await using var connection = new NpgsqlConnection(isolated.AdminConnectionString);
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand(
                "SELECT to_regclass('identity.\"NotificationReads\"') IS NOT NULL", connection);
            return (bool)(await command.ExecuteScalarAsync(ct))!;
        }

        async Task<bool[]> RuntimePrivilegesAsync()
        {
            await using var connection = new NpgsqlConnection(isolated.AdminConnectionString);
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand("""
                SELECT ARRAY[has_table_privilege('mt_app', 'identity."NotificationReads"', 'SELECT'),
                    has_table_privilege('mt_app', 'identity."NotificationReads"', 'INSERT'),
                    has_table_privilege('mt_app', 'identity."NotificationReads"', 'DELETE'),
                    has_table_privilege('mt_app', 'identity."NotificationReads"', 'UPDATE')]
                """, connection);
            return (bool[])(await command.ExecuteScalarAsync(ct))!;
        }
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No tenant.");
    }
}
