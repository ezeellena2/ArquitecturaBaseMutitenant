using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// La migración de la E9 (B5) saca de la política del asistente el tope diario, la retención y el presupuesto que nadie
/// lee: la política conserva AllowOwnKey, los límites vigentes de Plataforma no cambian y el Down vuelve a crear las
/// columnas vacías, sin los valores viejos.
/// </summary>
public sealed class AssistantPolicyDeadColumnsMigrationTests
{
    private static readonly string[] DeadColumns = ["DailyMessageLimit", "MonthlyBudgetAmount", "MonthlyBudgetCurrency", "RetentionDays"];

    [Fact]
    public async Task Up_drops_only_the_dead_columns_and_down_brings_them_back_empty()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var client = isolated.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(isolated, client, ct);
        var tenantId = await OrganizationJourney.StageAsync(isolated, owner, "Política vieja " + Guid.NewGuid().ToString("N")[..8], ct);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var db = new ApplicationDbContext(options.Options, new NoTenant());
        var migrator = db.GetService<IMigrator>();
        const string previous = "20261016184814_PlatformRateLimits";
        const string current = "20261016214625_AssistantPolicyDeadColumns";
        await migrator.MigrateAsync(previous, ct);
        var createdAtUtc = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO platform."AssistantOrganizationPolicies"
                ("Id", "OrganizationId", "AllowOwnKey", "DailyMessageLimit", "RetentionDays", "MonthlyBudgetAmount", "MonthlyBudgetCurrency", "CreatedAtUtc")
            VALUES ({Guid.CreateVersion7()}, {tenantId}, true, 7, 30, 12.5, 'USD', {createdAtUtc})
            ON CONFLICT ("OrganizationId") DO UPDATE SET "AllowOwnKey" = true, "DailyMessageLimit" = 7, "RetentionDays" = 30,
                "MonthlyBudgetAmount" = 12.5, "MonthlyBudgetCurrency" = 'USD';
            """, ct);
        var limits = await PlatformLimitsAsync();
        Assert.NotNull(limits);

        await migrator.MigrateAsync(current, ct);
        Assert.Empty(await DeadColumnsAsync());
        Assert.True(await AllowOwnKeyAsync());
        Assert.Equal(limits, await PlatformLimitsAsync());

        await migrator.MigrateAsync(previous, ct);
        Assert.Equal(DeadColumns.Select(column => column + " YES"), await DeadColumnsAsync());
        Assert.Equal(0, await db.Database.SqlQuery<int>($"""
            SELECT num_nonnulls("DailyMessageLimit", "RetentionDays", "MonthlyBudgetAmount", "MonthlyBudgetCurrency") AS "Value"
            FROM platform."AssistantOrganizationPolicies" WHERE "OrganizationId" = {tenantId}
            """).SingleAsync(ct));

        await migrator.MigrateAsync(current, ct);
        Assert.Empty(await DeadColumnsAsync());
        Assert.True(await AllowOwnKeyAsync());
        Assert.Equal(limits, await PlatformLimitsAsync());
        Assert.False(db.Database.HasPendingModelChanges());

        Task<bool> AllowOwnKeyAsync() => db.Set<AssistantOrganizationPolicy>().AsNoTracking()
            .Where(row => row.OrganizationId == tenantId).Select(row => row.AllowOwnKey).SingleAsync(ct);

        async Task<string[]> DeadColumnsAsync()
        {
            await using var connection = new NpgsqlConnection(isolated.AdminConnectionString);
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand("""
                SELECT column_name || ' ' || is_nullable FROM information_schema.columns
                WHERE table_schema = 'platform' AND table_name = 'AssistantOrganizationPolicies' AND column_name = ANY(@dead)
                ORDER BY column_name
                """, connection);
            command.Parameters.AddWithValue("dead", DeadColumns);
            var columns = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) columns.Add(reader.GetString(0));
            return [.. columns];
        }

        async Task<string?> PlatformLimitsAsync()
        {
            await using var connection = new NpgsqlConnection(isolated.AdminConnectionString);
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand(
                "SELECT row_to_json(settings)::text FROM platform.\"AssistantPlatformSettings\" settings", connection);
            return (string?)await command.ExecuteScalarAsync(ct);
        }
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No tenant.");
    }
}
