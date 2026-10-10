using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

public sealed class AssistantFinanceMigrationTests
{
    [Fact]
    public async Task Incremental_up_down_up_preserves_old_cost_and_restores_exact_grants_and_classification()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var db = new ApplicationDbContext(options.Options, new NoTenant());
        var migrator = db.GetService<IMigrator>();
        const string previous = "20261010103933_AssistantDurableConversations";
        const string current = "20261010124555_AssistantFinance";
        await migrator.MigrateAsync(previous, ct);
        var id = Guid.CreateVersion7();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO platform."PlatformAssistantUsageDaily"
                ("Id", "UserId", "Day", "InputTokens", "OutputTokens", "MessageCount", "CostAmount", "CostCurrency")
            VALUES ({id}, {Guid.CreateVersion7()}, {new DateOnly(2026,10,10)}, 1000, 100, 1, 0.123456, 'USD');
            """, ct);
        await migrator.MigrateAsync(current, ct);
        await AssertSchemaAsync(12);
        await migrator.MigrateAsync(previous, ct);
        await AssertSchemaAsync(6);
        await migrator.MigrateAsync(current, ct);
        await AssertSchemaAsync(12);
        Assert.False(db.Database.HasPendingModelChanges());

        async Task AssertSchemaAsync(int expectedScale)
        {
            await using var connection = new NpgsqlConnection(isolated.AdminConnectionString);
            await connection.OpenAsync(ct);
            await using var cost = new NpgsqlCommand("SELECT \"CostAmount\" FROM platform.\"PlatformAssistantUsageDaily\" WHERE \"Id\"=@id", connection);
            cost.Parameters.AddWithValue("id", id);
            Assert.Equal(.123456m, await cost.ExecuteScalarAsync(ct));
            await using var scale = new NpgsqlCommand("""
                SELECT numeric_scale FROM information_schema.columns WHERE column_name='CostAmount'
                AND (table_schema='platform' AND table_name='PlatformAssistantUsageDaily'
                    OR table_schema='tenant' AND table_name='AssistantUsageDaily');
                """, connection);
            await using (var reader = await scale.ExecuteReaderAsync(ct))
            {
                var count = 0;
                while (await reader.ReadAsync(ct)) { Assert.Equal(expectedScale, reader.GetInt32(0)); count++; }
                Assert.Equal(2, count);
            }
            if (expectedScale == 6) return;
            await using var tables = new NpgsqlCommand("""
                SELECT n.nspname, c.relname, c.relrowsecurity, c.relforcerowsecurity,
                    has_table_privilege('mt_app', c.oid, 'SELECT') AND has_table_privilege('mt_app', c.oid, 'INSERT')
                    AND has_table_privilege('mt_app', c.oid, 'UPDATE') AND has_table_privilege('mt_app', c.oid, 'DELETE')
                FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                WHERE (n.nspname='platform' AND c.relname IN ('AssistantBudgetMonths','AssistantFinancialOperations','AssistantFinancialCalls','AssistantPlatformKeyDailyQuotas'))
                    OR (n.nspname='tenant' AND c.relname IN ('AssistantOwnKeyDailyQuotas','AssistantUsageFacts'));
                """, connection);
            await using var tableReader = await tables.ExecuteReaderAsync(ct);
            var tableCount = 0;
            while (await tableReader.ReadAsync(ct))
            {
                var isPrivate = tableReader.GetString(0) == "tenant";
                Assert.Equal(isPrivate, tableReader.GetBoolean(2));
                Assert.Equal(isPrivate, tableReader.GetBoolean(3));
                Assert.True(tableReader.GetBoolean(4), tableReader.GetString(1)); tableCount++;
            }
            Assert.Equal(6, tableCount);
        }
    }
    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No tenant.");
    }
}
