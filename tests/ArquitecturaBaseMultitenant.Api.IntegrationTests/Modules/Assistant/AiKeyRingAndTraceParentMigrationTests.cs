using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// Revisión adversarial de la B15 (guía de migración): el Down de <c>AiKeyRingAndTraceParent</c> borra
/// <c>platform.AiKeyRingControls</c> sin mirar si hay controles. Con uno guardado, tiene que negarse antes de cualquier DDL,
/// como <c>AssistantEngineVersionAndDailyQuotaMigrationTests</c>: si no, un Down y un Up dejan que la próxima copia, con
/// cualquier anillo, escriba los controles que manda (0064 §11: nunca se borra un control para que pase otro anillo).
/// </summary>
public sealed class AiKeyRingAndTraceParentMigrationTests
{
    [Fact]
    public async Task Down_refuses_to_drop_the_key_ring_controls()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var db = new ApplicationDbContext(options.Options, new NoTenant());
        var migrator = db.GetService<IMigrator>();
        const string previous = "20261125105915_AssistantEngineVersionAndDailyQuota";
        const string current = "20261125121605_AiKeyRingAndTraceParent";

        await migrator.MigrateAsync(previous, ct);
        await migrator.MigrateAsync(current, ct);
        var keyId = Guid.NewGuid();
        byte[] control = [1, 2, 3];
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO platform."AiKeyRingControls" ("KeyId", "Control", "CreatedAtUtc")
            VALUES ({keyId}, {control}, {new DateTime(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc)})
            """, ct);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(previous, ct));
        Assert.Equal(PostgresErrorCodes.RaiseException, refused.SqlState);
        Assert.Contains(current, await db.Database.GetAppliedMigrationsAsync(ct));
        Assert.Equal(1, await db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value" FROM platform."AiKeyRingControls" WHERE "KeyId" = {keyId}
            """).SingleAsync(ct));
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No tenant.");
    }
}
