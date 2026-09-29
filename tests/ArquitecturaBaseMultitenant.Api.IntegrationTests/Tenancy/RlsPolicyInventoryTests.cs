using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

[Collection(ApiTestGroup.Name)]
public sealed class RlsPolicyInventoryTests(ApiFactory factory)
{
    [Fact]
    public async Task Existing_audit_trigger_uses_an_unquoted_tenant_column_argument()
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT encode(t.tgargs, 'escape')
            FROM pg_trigger t
            WHERE t.tgrelid = 'tenant."AuditEntries"'::regclass
              AND t.tgname = 'prevent_tenant_change'
            """, connection);

        var arguments = (string?)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        Assert.NotNull(arguments);
        Assert.StartsWith("TenantId", arguments, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_protected_table_has_forced_rls_and_its_class_policy()
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT n.nspname, c.relname, c.relrowsecurity, c.relforcerowsecurity,
                   COALESCE((SELECT string_agg(p.policyname::text, ',' ORDER BY p.policyname)
                             FROM pg_policies p
                             WHERE p.schemaname = n.nspname AND p.tablename = c.relname), '')
            FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname IN ('tenant', 'public_site', 'engagement') AND c.relkind IN ('r', 'p')
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var tables = new Dictionary<string, string[]>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            var schema = reader.GetString(0);
            var table = reader.GetString(1);
            Assert.True(reader.GetBoolean(2), $"{schema}.{table} has RLS disabled.");
            Assert.True(reader.GetBoolean(3), $"{schema}.{table} does not force RLS.");
            var policies = reader.GetString(4).Split(',', StringSplitOptions.RemoveEmptyEntries);
            Assert.NotEmpty(policies);
            tables.Add($"{schema}.{table}", policies);
        }

        Assert.Contains("tenant.AuditEntries", tables.Keys);
        Assert.Contains("tenant.Widgets", tables.Keys);
        Assert.Contains("public_site.Posters", tables.Keys);
        Assert.Contains("engagement.Deals", tables.Keys);
        foreach (var (table, policies) in tables)
        {
            if (table.StartsWith("tenant.", StringComparison.Ordinal))
            {
                Assert.Contains("tenant_scope", policies);
            }
            else if (table.StartsWith("public_site.", StringComparison.Ordinal))
            {
                Assert.Contains("public_read", policies);
                Assert.Contains("public_insert", policies);
                Assert.Contains("public_update", policies);
                Assert.Contains("public_delete", policies);
            }
            else
            {
                Assert.Contains("parties_scope", policies);
            }
        }
    }
}
