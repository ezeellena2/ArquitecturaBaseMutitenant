using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

public sealed class AssistantEditableCatalogMigrationTests
{
    [Fact]
    public async Task Upgrade_preserves_prices_settings_and_unknown_rows_with_audited_known_backfill_and_real_down_up()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        await isolated.Services.SeedDatabaseAsync(ct);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var db = new ApplicationDbContext(options.Options, new NoTenant());
        var migrator = db.GetService<IMigrator>();
        const string previous = "20261010214712_PlatformOperatorAssistantPermissions";
        const string current = "20261010223305_AssistantEditableModelCatalog";
        var originalSettings = await db.Set<AssistantPlatformSettings>().AsNoTracking().SingleAsync(ct);
        var originalIds = await db.Set<AssistantModelPrice>().OrderBy(value => value.Id).Select(value => value.Id).ToArrayAsync(ct);
        await migrator.MigrateAsync(previous, ct);
        var legacy = Guid.CreateVersion7();
        await db.Database.ExecuteSqlAsync($"""
            UPDATE platform."AssistantModelPrices" SET "InputPriceAmount"=0.123456, "DisplayName"='Edited before upgrade'
                WHERE "ProviderModelId"='claude-sonnet-5-5';
            INSERT INTO platform."AssistantModelPrices"
                ("Id", "Provider", "ProviderModelId", "DisplayName", "InputPriceAmount", "InputPriceCurrency",
                 "OutputPriceAmount", "OutputPriceCurrency", "IsEnabled", "CreatedAtUtc")
                VALUES ({legacy}, 'Anthropic', 'legacy-unknown', 'Legacy unknown', 0.654321, 'USD', 2, 'USD', true, CURRENT_TIMESTAMP);
            """, ct);
        await migrator.MigrateAsync(current, ct);
        await AssertUpgradeAsync(4);
        await migrator.MigrateAsync(current, ct);
        await AssertUpgradeAsync(4);
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE platform."AssistantModelPrices" SET "TechnicalCacheReadAmount"=0.333333
                WHERE "ProviderModelId"='claude-sonnet-5-5';
            """, ct);
        await migrator.MigrateAsync(previous, ct);
        await migrator.MigrateAsync(current, ct);
        await AssertUpgradeAsync(8); // Down discards later technical edits, explicitly; base prices survive.
        Assert.False(db.Database.HasPendingModelChanges());

        async Task AssertUpgradeAsync(int auditCount)
        {
            var rows = await db.Set<AssistantModelPrice>().AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(ct);
            Assert.Equal(originalIds, rows.Where(value => value.Id != legacy).Select(value => value.Id));
            var unknown = Assert.Single(rows, value => value.Id == legacy);
            Assert.Null(unknown.Technical);
            Assert.Equal(.654321m, unknown.InputPrice.Amount);
            var known = Assert.Single(rows, value => value.ProviderModelId == "claude-sonnet-5-5");
            Assert.Equal(.123456m, known.InputPrice.Amount);
            Assert.Equal("Edited before upgrade", known.DisplayName);
            Assert.Equal(1000000, known.Technical!.MaxInputTokens);
            Assert.Equal(128000, known.Technical.MaxOutputTokens);
            Assert.Equal(.1m, known.Technical.CacheRead.Amount);
            var settings = await db.Set<AssistantPlatformSettings>().AsNoTracking().SingleAsync(ct);
            Assert.Equal(originalSettings.Id, settings.Id);
            Assert.Equal(originalSettings.Version, settings.Version);
            Assert.Equal(originalSettings.DefaultModelId, settings.DefaultModelId);
            Assert.Equal(originalSettings.AllowedModelIds, settings.AllowedModelIds);
            Assert.Equal(auditCount, await db.SecurityEvents.CountAsync(value => value.Reason == "Assistant technical catalog upgrade", ct));
            using var scope = isolated.Services.CreateScope();
            var projections = await scope.ServiceProvider.GetRequiredService<IAssistantModelReader>().ListAllAsync(ct);
            var projection = Assert.Single(projections, value => value.Id == legacy);
            Assert.Null(projection.Technical);
            Assert.Null(projection.TechnicalRevision);
            Assert.False(AssistantModelCompatibilityPolicy.IsCompatible(projection));
            await using var connection = new NpgsqlConnection(isolated.AdminConnectionString);
            await connection.OpenAsync(ct);
            await using var guard = new NpgsqlCommand("""
                SELECT NOT (SELECT rolbypassrls FROM pg_roles WHERE rolname='mt_owner')
                    AND NOT c.relrowsecurity AND NOT c.relforcerowsecurity
                    AND has_table_privilege('mt_app', c.oid, 'SELECT,INSERT,UPDATE,DELETE')
                FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                WHERE n.nspname='platform' AND c.relname='AssistantModelPrices';
                """, connection);
            Assert.Equal(true, await guard.ExecuteScalarAsync(ct));
            await using var tariffs = new NpgsqlCommand("""
                SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema='platform' AND table_name='AssistantModelPrices'
                    AND column_name LIKE 'Technical%Amount' AND numeric_precision=18 AND numeric_scale=6;
                """, connection);
            Assert.Equal(8L, await tariffs.ExecuteScalarAsync(ct));
            await using var currency = new NpgsqlCommand("""
                UPDATE platform."AssistantModelPrices" SET "TechnicalCacheReadCurrency"='XXX'
                WHERE "ProviderModelId"='claude-sonnet-5-5';
                """, connection);
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, (await Assert.ThrowsAsync<PostgresException>(() => currency.ExecuteNonQueryAsync(ct))).SqlState);
        }
    }
    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No tenant.");
    }
}
