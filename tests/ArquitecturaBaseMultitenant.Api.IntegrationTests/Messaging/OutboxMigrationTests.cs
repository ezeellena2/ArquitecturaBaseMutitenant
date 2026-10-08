using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
}
