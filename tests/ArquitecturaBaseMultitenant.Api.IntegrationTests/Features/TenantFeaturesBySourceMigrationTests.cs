using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Features;

/// <summary>
/// B1 (ADR 0069): el índice único de los módulos pasa a ser por origen. Una fila guardada antes sigue igual, la base admite
/// una fila manual y una de suscripción del mismo módulo y rechaza dos manuales, y el <c>Down</c> se niega, sin tocar nada,
/// mientras un módulo tenga dos filas en una organización.
/// </summary>
public sealed class TenantFeaturesBySourceMigrationTests
{
    private const string Previous = "20261125153522_SettingsRules";
    private const string Current = "20261126150701_TenantFeaturesBySource";
    private const string OldIndex = "IX_TenantFeatures_TenantId_FeatureKey";

    [Fact]
    public async Task A_row_saved_before_keeps_working_and_the_index_admits_one_row_per_source()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        await using var context = CreateContext(isolated);
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(Previous, ct);
        var tenantId = await InsertTenantAsync(context, ct);
        var saved = await InsertFeatureAsync(context, tenantId, "whatsapp", "Manual", ct);

        await migrator.MigrateAsync(Current, ct);
        var row = await context.TenantFeatures.AsNoTracking().SingleAsync(value => value.Id == saved, ct);
        Assert.Equal((tenantId, "whatsapp", true, TenantFeatureSource.Manual), (row.TenantId, row.FeatureKey, row.Enabled, row.Source));
        Assert.Null(row.ExpiresAtUtc);
        Assert.NotEqual(0u, row.Version);
        Assert.Equal([UniqueKeys.TenantFeature], await UniqueIndexesAsync(context, ct));

        await InsertFeatureAsync(context, tenantId, "whatsapp", "Subscription", ct);
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => InsertFeatureAsync(context, tenantId, "whatsapp", "Manual", ct));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
        Assert.Equal(UniqueKeys.TenantFeature, duplicate.ConstraintName);
        Assert.Equal(2, await context.TenantFeatures.CountAsync(value => value.TenantId == tenantId && value.FeatureKey == "whatsapp", ct));
    }

    [Fact]
    public async Task Down_refuses_while_a_module_has_two_rows_and_goes_back_once_it_has_one()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        await using var context = CreateContext(isolated);
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(Current, ct);
        var tenantId = await InsertTenantAsync(context, ct);
        var otherTenantId = await InsertTenantAsync(context, ct);
        var manual = await InsertFeatureAsync(context, tenantId, "whatsapp", "Manual", ct);
        var subscription = await InsertFeatureAsync(context, tenantId, "whatsapp", "Subscription", ct);
        // Un mismo módulo en dos organizaciones, y dos módulos en una, no son filas repetidas.
        await InsertFeatureAsync(context, tenantId, "assistant", "Manual", ct);
        await InsertFeatureAsync(context, otherTenantId, "whatsapp", "Manual", ct);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(Previous, ct));
        Assert.Equal(PostgresErrorCodes.RaiseException, refused.SqlState);
        Assert.Contains(Current, await context.Database.GetAppliedMigrationsAsync(ct));
        Assert.Equal(4, await context.TenantFeatures.CountAsync(value => value.TenantId == tenantId || value.TenantId == otherTenantId, ct));
        Assert.Equal([UniqueKeys.TenantFeature], await UniqueIndexesAsync(context, ct));

        await context.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM platform."TenantFeatures" WHERE "Id" = {subscription}""", ct);
        await migrator.MigrateAsync(Previous, ct);
        Assert.DoesNotContain(Current, await context.Database.GetAppliedMigrationsAsync(ct));
        Assert.Equal([OldIndex], await UniqueIndexesAsync(context, ct));
        Assert.True(await context.TenantFeatures.AnyAsync(value => value.Id == manual, ct));

        await migrator.MigrateAsync(Current, ct);
        Assert.Equal([UniqueKeys.TenantFeature], await UniqueIndexesAsync(context, ct));
        Assert.Equal(3, await context.TenantFeatures.CountAsync(value => value.TenantId == tenantId || value.TenantId == otherTenantId, ct));
    }

    private static ApplicationDbContext CreateContext(ApiFactory isolated)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        return new ApplicationDbContext(options.Options, new TenantContext());
    }

    private static async Task<Guid> InsertTenantAsync(ApplicationDbContext context, CancellationToken ct)
    {
        var id = Guid.CreateVersion7();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO platform."Tenants" ("Id", "Name", "Kind", "Status", "RegistrationSource")
            VALUES ({id}, 'Módulos por origen', 'Business', 'Active', 'SelfService');
            """, ct);
        return id;
    }

    private static async Task<Guid> InsertFeatureAsync(ApplicationDbContext context, Guid tenantId, string key, string source, CancellationToken ct)
    {
        var id = Guid.CreateVersion7();
        var nowUtc = TimeProvider.System.GetUtcNow().UtcDateTime;
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO platform."TenantFeatures" ("Id", "TenantId", "FeatureKey", "Enabled", "Source", "CreatedAtUtc")
            VALUES ({id}, {tenantId}, {key}, TRUE, {source}, {nowUtc});
            """, ct);
        return id;
    }

    private static Task<List<string>> UniqueIndexesAsync(ApplicationDbContext context, CancellationToken ct) =>
        context.Database.SqlQuery<string>($"""
            SELECT indexname AS "Value" FROM pg_indexes
            WHERE schemaname = 'platform' AND tablename = 'TenantFeatures' AND indexdef LIKE 'CREATE UNIQUE INDEX%' AND indexname <> 'PK_TenantFeatures'
            ORDER BY indexname
            """).ToListAsync(ct);
}
