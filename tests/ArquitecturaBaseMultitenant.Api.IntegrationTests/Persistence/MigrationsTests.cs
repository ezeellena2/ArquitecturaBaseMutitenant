using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

public sealed class MigrationsTests
{
    [Fact]
    public void Current_model_has_no_pending_migration_changes()
    {
        using var context = CreateContext();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Initial_migration_creates_e2_schema_without_business_test_tables_or_identity_users()
    {
        using var context = CreateContext();
        var script = context.GetService<IMigrator>().GenerateScript();

        foreach (var schema in new[] { "platform", "identity", "tenant", "public_site", "engagement" })
        {
            Assert.Contains($"CREATE SCHEMA {schema};", script, StringComparison.Ordinal);
        }

        foreach (var table in new[]
        {
            "Currencies", "CurrencyTranslations", "Countries", "CountryTranslations", "TimeZones",
            "TimeZoneCountries", "TimeZoneTranslations", "Cultures", "CultureTranslations", "TaxIdTypes",
            "TaxIdTypeTranslations"
        })
        {
            Assert.Contains($"CREATE TABLE platform.\"{table}\"", script, StringComparison.Ordinal);
        }

        Assert.Contains("CREATE TABLE platform.\"IdempotencyKeys\"", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE tenant.\"AuditEntries\"", script, StringComparison.Ordinal);
        Assert.Contains("unaccent", script, StringComparison.Ordinal);
        Assert.Contains("pg_trgm", script, StringComparison.Ordinal);
        Assert.Contains("f_unaccent", script, StringComparison.Ordinal);
        Assert.Contains("prevent_tenant_change", script, StringComparison.Ordinal);
        Assert.Contains("prevent_update_delete", script, StringComparison.Ordinal);
        Assert.Contains("FORCE ROW LEVEL SECURITY", script, StringComparison.Ordinal);
        Assert.Contains("mt_app", script, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetUsers", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Widgets", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Posters", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Deals", script, StringComparison.Ordinal);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=migration_model_test")
            .Options;
        return new ApplicationDbContext(options, new TestTenantContext());
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No tenant.");
    }
}
