using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// La migración del motivo de un fallo en la cola durable de la entrada (B5, revisión de la etapa): el Up suma la columna, el
/// Down vuelve atrás sin ella y, con un fallo de una organización que la cola todavía no anotó, se niega antes que perderlo. Uno
/// ya anotado no la frena: su motivo quedó en el chat o en la difusión.
/// </summary>
public sealed class WhatsAppMessageFailureReasonMigrationTests
{
    [Fact]
    public async Task Down_goes_back_without_the_column_and_refuses_to_lose_a_pending_failure()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var db = new ApplicationDbContext(options.Options, new NoTenant());
        var migrations = db.Database.GetMigrations().ToArray();
        var index = Array.FindIndex(migrations, name => name.EndsWith("_WhatsAppMessageFailureReason", StringComparison.Ordinal));
        Assert.True(index > 0);
        var previous = migrations[index - 1];
        var current = migrations[index];
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync(previous, ct);
        Assert.False(await ColumnExistsAsync());
        await migrator.MigrateAsync(current, ct);
        Assert.True(await ColumnExistsAsync());

        // Sin fallos pendientes, va y vuelve.
        Assert.Equal(1, await RunAsync("""
            INSERT INTO identity."WhatsAppMessages" ("Id", "PhoneNumberId", "Wamid", "EventType", "Direction", "Kind", "Status",
                "ReceivedAtUtc", "OccurredAtUtc", "Attempts", "FailureReason", "ProcessedAtUtc")
            VALUES (gen_random_uuid(), 'migration-number', 'wamid.migration.done', 'delivery_failure', 'Outbound', 'Delivery', 'Processed',
                now(), now(), 1, 'RateLimited', now())
            """));
        await migrator.MigrateAsync(previous, ct);
        Assert.False(await ColumnExistsAsync());
        await migrator.MigrateAsync(current, ct);

        // Un fallo que la cola todavía no anotó frena el Down y queda como estaba.
        Assert.Equal(1, await RunAsync("""
            INSERT INTO identity."WhatsAppMessages" ("Id", "PhoneNumberId", "Wamid", "EventType", "Direction", "Kind", "Status",
                "ReceivedAtUtc", "OccurredAtUtc", "Attempts", "FailureReason", "NextAttemptAtUtc")
            VALUES (gen_random_uuid(), 'migration-number', 'wamid.migration.pending', 'delivery_failure', 'Outbound', 'Delivery', 'Pending',
                now(), now(), 0, 'Undeliverable', now())
            """));
        var refused = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(previous, ct));
        Assert.Equal(PostgresErrorCodes.RaiseException, refused.SqlState);
        Assert.True(await ColumnExistsAsync());
        Assert.Contains(current, await db.Database.GetAppliedMigrationsAsync(ct));

        Assert.Equal(1, await RunAsync("""
            UPDATE identity."WhatsAppMessages" SET "ProcessedAtUtc" = now(), "Status" = 'Processed', "NextAttemptAtUtc" = NULL
            WHERE "Wamid" = 'wamid.migration.pending'
            """));
        await migrator.MigrateAsync(previous, ct);
        Assert.False(await ColumnExistsAsync());
        await migrator.MigrateAsync(cancellationToken: ct);
        Assert.False(db.Database.HasPendingModelChanges());

        async Task<bool> ColumnExistsAsync()
        {
            await using var connection = new NpgsqlConnection(isolated.AdminConnectionString);
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand("""
                SELECT EXISTS (SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'identity' AND table_name = 'WhatsAppMessages' AND column_name = 'FailureReason')
                """, connection);
            return (bool)(await command.ExecuteScalarAsync(ct))!;
        }

        async Task<int> RunAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(isolated.AdminConnectionString);
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand(sql, connection);
            return await command.ExecuteNonQueryAsync(ct);
        }
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No tenant.");
    }
}
