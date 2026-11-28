using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Messaging;

/// <summary>Upgrade de datos históricos: limpiar solo terminales conserva los reintentos de todos los canales.</summary>
public sealed class OutboxMigrationTests
{
    [Fact]
    public async Task Upgrade_clears_terminal_payloads_and_preserves_pending_and_retryable_messages()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var context = new ApplicationDbContext(options.Options, new TenantContext());
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20261008184407_WhatsAppIdentity", ct);
        var nowUtc = TimeProvider.System.GetUtcNow().UtcDateTime;
        var expected = new Dictionary<Guid, bool>();
        foreach (var channel in new[] { "email", "whatsapp" })
            foreach (var (status, retryable) in new[] { ("Sent", false), ("Cancelled", false), ("Failed", false), ("Failed", true), ("Pending", true) })
            {
                var id = Guid.CreateVersion7(); expected.Add(id, retryable);
                DateTime? next = retryable ? nowUtc : null;
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO platform."OutboxMessages" ("Id", "Channel", "EncryptedPayload", "Status", "Attempts", "CreatedAtUtc", "NextAttemptAtUtc")
                    VALUES ({id}, {channel}, 'historic-cipher', {status}, 0, {nowUtc}, {next});
                    """, ct);
            }
        await migrator.MigrateAsync(cancellationToken: ct);
        foreach (var row in await context.OutboxMessages.AsNoTracking().Where(value => expected.Keys.Contains(value.Id)).ToArrayAsync(ct))
            Assert.Equal(expected[row.Id] ? "historic-cipher" : string.Empty, row.EncryptedPayload);
        Assert.False(context.Database.HasPendingModelChanges());
    }

    /// <summary>
    /// ADR 0066: el Up suma el orden, la prioridad y el plazo, y pasa a pendiente lo que esperaba un reintento; el Down vuelve
    /// sin filas «Sin confirmar» y, con alguna, se niega antes de borrar nada, porque la versión anterior no la puede leer.
    /// </summary>
    [Fact]
    public async Task Uncertain_and_ordering_go_up_and_down_and_refuse_to_lose_an_unconfirmed_send()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var context = new ApplicationDbContext(options.Options, new TenantContext());
        var migrator = context.GetService<IMigrator>();
        const string previous = "20261124215753_MessageQuotaBuckets";
        const string current = "20261124235254_OutboxUncertainAndOrdering";
        await migrator.MigrateAsync(previous, ct);
        var nowUtc = TimeProvider.System.GetUtcNow().UtcDateTime;
        var retrying = Guid.CreateVersion7();
        var failed = Guid.CreateVersion7();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO platform."OutboxMessages" ("Id", "Channel", "EncryptedPayload", "Status", "Attempts", "CreatedAtUtc", "NextAttemptAtUtc")
            VALUES ({retrying}, 'email', 'cipher', 'Failed', 1, {nowUtc}, {nowUtc}), ({failed}, 'email', '', 'Failed', 5, {nowUtc}, NULL);
            """, ct);

        await migrator.MigrateAsync(current, ct);
        var rows = await context.OutboxMessages.AsNoTracking()
            .Where(value => value.Id == retrying || value.Id == failed)
            .Select(value => new { value.Id, value.Status, value.Priority, value.OrderingKey })
            .ToDictionaryAsync(value => value.Id, ct);
        Assert.Equal(OutboxStatus.Pending, rows[retrying].Status);
        Assert.Equal(OutboxStatus.Failed, rows[failed].Status);
        Assert.All(rows.Values, row => Assert.Equal(OutboxPriority.Notice, row.Priority));
        Assert.All(rows.Values, row => Assert.Null(row.OrderingKey));
        // Lo que inserta la versión anterior, sin la columna, queda como aviso.
        var legacy = Guid.CreateVersion7();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO platform."OutboxMessages" ("Id", "Channel", "EncryptedPayload", "Status", "Attempts", "CreatedAtUtc", "NextAttemptAtUtc")
            VALUES ({legacy}, 'email', 'cipher', 'Pending', 0, {nowUtc}, {nowUtc});
            """, ct);
        Assert.Equal(OutboxPriority.Notice, await context.OutboxMessages.AsNoTracking()
            .Where(value => value.Id == legacy)
            .Select(value => value.Priority)
            .SingleAsync(ct));

        var uncertain = Guid.CreateVersion7();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO platform."OutboxMessages" ("Id", "Channel", "EncryptedPayload", "Status", "Attempts", "CreatedAtUtc", "Priority")
            VALUES ({uncertain}, 'email', '', 'Uncertain', 1, {nowUtc}, 0);
            """, ct);
        var refused = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(previous, ct));
        Assert.Equal(PostgresErrorCodes.RaiseException, refused.SqlState);
        Assert.Contains(current, await context.Database.GetAppliedMigrationsAsync(ct));

        await context.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM platform."OutboxMessages" WHERE "Id" = {uncertain}""", ct);
        await migrator.MigrateAsync(previous, ct);
        Assert.DoesNotContain(current, await context.Database.GetAppliedMigrationsAsync(ct));
        await migrator.MigrateAsync(current, ct);
        Assert.False(context.Database.HasPendingModelChanges());
    }
}
