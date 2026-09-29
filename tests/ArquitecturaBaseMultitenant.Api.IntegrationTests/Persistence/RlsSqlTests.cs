using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

public sealed class RlsSqlTests
{
    [Fact]
    public void Private_policy_forces_rls_and_checks_writes()
    {
        var sql = RlsSql.EnableTenantRls("tenant", "Widgets");

        Assert.Contains("FORCE ROW LEVEL SECURITY", sql, StringComparison.Ordinal);
        Assert.Contains("nullif(current_setting('app.tenant_id', true), '')::uuid", sql, StringComparison.Ordinal);
        Assert.Contains("WITH CHECK", sql, StringComparison.Ordinal);
        Assert.Contains("prevent_tenant_change", sql, StringComparison.Ordinal);
        Assert.Contains("\"TenantId\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"prevent_tenant_change\"('TenantId')", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Published_foreign_row_is_readable_but_never_writable()
    {
        var sql = RlsSql.EnablePublicRls("public_site", "Posters");

        Assert.Contains("FOR SELECT", sql, StringComparison.Ordinal);
        Assert.Contains("\"IsPublished\"", sql, StringComparison.Ordinal);
        Assert.Contains("FOR INSERT", sql, StringComparison.Ordinal);
        Assert.Contains("FOR UPDATE", sql, StringComparison.Ordinal);
        Assert.Contains("FOR DELETE", sql, StringComparison.Ordinal);
        Assert.Contains("WITH CHECK", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("FOR ALL", sql, StringComparison.Ordinal);
        Assert.Contains("prevent_tenant_change", sql, StringComparison.Ordinal);
        Assert.Contains("\"prevent_tenant_change\"('BusinessTenantId')", sql, StringComparison.Ordinal);

        var updatePolicy = sql.Split("CREATE POLICY public_update", StringSplitOptions.None)[1].Split(';')[0];
        var deletePolicy = sql.Split("CREATE POLICY public_delete", StringSplitOptions.None)[1].Split(';')[0];
        Assert.DoesNotContain("IsPublished", updatePolicy, StringComparison.Ordinal);
        Assert.DoesNotContain("IsPublished", deletePolicy, StringComparison.Ordinal);
    }

    [Fact]
    public void Shared_policy_limits_rows_to_the_two_participants()
    {
        var sql = RlsSql.EnablePartiesRls("engagement", "Deals");

        Assert.Contains("\"ConsumerTenantId\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"BusinessTenantId\"", sql, StringComparison.Ordinal);
        Assert.Contains(" WITH CHECK ", sql, StringComparison.Ordinal);
        Assert.Contains("FORCE ROW LEVEL SECURITY", sql, StringComparison.Ordinal);
        Assert.Contains("prevent_tenant_change", sql, StringComparison.Ordinal);
        Assert.Contains("\"prevent_tenant_change\"('ConsumerTenantId', 'BusinessTenantId')", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Audit_trigger_rejects_updates_and_deletes()
    {
        var sql = RlsSql.PreventUpdateDelete("tenant", "AuditEntries");

        Assert.Contains("BEFORE UPDATE OR DELETE", sql, StringComparison.Ordinal);
        Assert.Contains("prevent_update_delete", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Support_functions_reject_tenant_column_changes_and_audit_mutations()
    {
        var sql = RlsSql.CreateSupportFunctions();

        Assert.Contains("TG_ARGV", sql, StringComparison.Ordinal);
        Assert.Contains("IS DISTINCT FROM", sql, StringComparison.Ordinal);
        Assert.Contains("RAISE EXCEPTION", sql, StringComparison.Ordinal);
        Assert.Contains("prevent_update_delete", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("tenant; DROP SCHEMA platform CASCADE", "Widgets")]
    [InlineData("tenant", "Widgets\"; DROP TABLE tenant.Other; --")]
    [InlineData("", "Widgets")]
    public void Identifiers_cannot_inject_sql(string schema, string table)
    {
        Assert.Throws<ArgumentException>(() => RlsSql.EnableTenantRls(schema, table));
    }

    [Fact]
    public void Migration_extensions_use_the_same_sql_as_isolation_schema()
    {
        var migration = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        migration.EnableTenantRls("tenant", "Widgets");

        var operation = Assert.IsType<SqlOperation>(Assert.Single(migration.Operations));
        Assert.Equal(RlsSql.EnableTenantRls("tenant", "Widgets"), operation.Sql);
    }
}
