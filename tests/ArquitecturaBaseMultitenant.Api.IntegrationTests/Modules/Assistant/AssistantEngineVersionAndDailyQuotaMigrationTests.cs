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
/// Revisión adversarial de la B14 (guía de migración): el Down de <c>AssistantEngineVersionAndDailyQuota</c> borra la
/// organización de cada cupo sin mirar si hay alguno. Con un cupo de una organización guardado, tiene que negarse antes de
/// cualquier DDL, como <c>NotificationReadsMigrationTests</c>, en vez de convertirlo en el cupo de fuera de una.
/// </summary>
public sealed class AssistantEngineVersionAndDailyQuotaMigrationTests
{
    [Fact]
    public async Task Down_refuses_to_lose_the_organization_of_a_daily_quota()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var db = new ApplicationDbContext(options.Options, new NoTenant());
        var migrator = db.GetService<IMigrator>();
        const string previous = "20261125053404_AssistantProviderIssue";
        const string current = "20261125105915_AssistantEngineVersionAndDailyQuota";

        await migrator.MigrateAsync(previous, ct);
        await migrator.MigrateAsync(current, ct);
        var userId = Guid.CreateVersion7();
        var organizationId = Guid.CreateVersion7();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO platform."AssistantPlatformKeyDailyQuotas" ("Id", "UserId", "OrganizationId", "DayUtc", "ReservedQueries", "ConsumedQueries")
            VALUES ({Guid.CreateVersion7()}, {userId}, {organizationId}, {new DateOnly(2026, 11, 25)}, 0, 7)
            """, ct);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(previous, ct));
        Assert.Equal(PostgresErrorCodes.RaiseException, refused.SqlState);
        Assert.Contains(current, await db.Database.GetAppliedMigrationsAsync(ct));
        Assert.Equal(organizationId, await db.Database.SqlQuery<Guid>($"""
            SELECT "OrganizationId" AS "Value" FROM platform."AssistantPlatformKeyDailyQuotas" WHERE "UserId" = {userId}
            """).SingleAsync(ct));
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No tenant.");
    }
}
