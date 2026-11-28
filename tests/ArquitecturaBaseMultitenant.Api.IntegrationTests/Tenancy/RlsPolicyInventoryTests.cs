using System.Text;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// Comprueba RLS forzado, comandos y expresiones de las políticas de cada tabla protegida, los triggers que
/// impiden cambiar el espacio de una fila y las protecciones de solo-agregar de las tablas de historial.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed partial class RlsPolicyInventoryTests(ApiFactory factory)
{
    private const string TenantSetting = "nullif current_setting 'app.tenant_id', true, ''::uuid";
    private const string TenantOwnsExpression = $"\"tenantid\" = {TenantSetting}";
    private const string BusinessOwnsExpression = $"\"businesstenantid\" = {TenantSetting}";
    private const string PublicReadExpression = $"\"ispublished\" or {BusinessOwnsExpression}";
    private const string PartiesExpression =
        $"\"consumertenantid\" = {TenantSetting} or \"businesstenantid\" = {TenantSetting}";

    private sealed record PolicyRow(
        string Name,
        string Permissive,
        string[] Roles,
        string Command,
        string? Qual,
        string? WithCheck);

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
    public async Task Every_protected_table_has_the_prevent_tenant_change_trigger_with_unquoted_columns()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(ct);

        var protectedTables = await LoadProtectedTablesAsync(connection, ct);
        Assert.NotEmpty(protectedTables);

        var triggers = new Dictionary<string, (string FunctionName, string Enabled, string[] Arguments)>(StringComparer.Ordinal);
        await using (var command = new NpgsqlCommand("""
            SELECT n.nspname, c.relname, p.proname, t.tgenabled::text, t.tgargs
            FROM pg_trigger t
            JOIN pg_class c ON c.oid = t.tgrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            JOIN pg_proc p ON p.oid = t.tgfoid
            WHERE NOT t.tgisinternal
              AND n.nspname IN ('tenant', 'public_site', 'engagement')
              AND c.relkind IN ('r', 'p')
              AND t.tgname = 'prevent_tenant_change'
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var key = $"{reader.GetString(0)}.{reader.GetString(1)}";
                var args = Encoding.UTF8.GetString(reader.GetFieldValue<byte[]>(4))
                    .Split('\0', StringSplitOptions.RemoveEmptyEntries);
                triggers[key] = (reader.GetString(2), reader.GetString(3), args);
            }
        }

        foreach (var table in protectedTables)
        {
            Assert.True(triggers.TryGetValue(table, out var trigger),
                $"{table} is missing the prevent_tenant_change trigger.");
            Assert.Equal("prevent_tenant_change", trigger.FunctionName);
            Assert.NotEqual("D", trigger.Enabled);

            string[] expectedColumns = table.StartsWith("tenant.", StringComparison.Ordinal)
                ? ["TenantId"]
                : table.StartsWith("public_site.", StringComparison.Ordinal)
                    ? ["BusinessTenantId"]
                    : ["ConsumerTenantId", "BusinessTenantId"];
            Assert.Equal(expectedColumns, trigger.Arguments);
        }
    }

    [Fact]
    public async Task Every_protected_table_has_forced_rls_and_its_class_policy()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(ct);

        var tables = new Dictionary<string, List<PolicyRow>>(StringComparer.Ordinal);
        await using (var command = new NpgsqlCommand("""
            SELECT n.nspname, c.relname, c.relrowsecurity, c.relforcerowsecurity
            FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname IN ('tenant', 'public_site', 'engagement') AND c.relkind IN ('r', 'p')
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var schema = reader.GetString(0);
                var table = reader.GetString(1);
                Assert.True(reader.GetBoolean(2), $"{schema}.{table} has RLS disabled.");
                Assert.True(reader.GetBoolean(3), $"{schema}.{table} does not force RLS.");
                tables.Add($"{schema}.{table}", []);
            }
        }

        await using (var command = new NpgsqlCommand("""
            SELECT p.schemaname, p.tablename, p.policyname, p.permissive,
                   COALESCE(array_to_string(p.roles, ','), ''),
                   p.cmd, p.qual, p.with_check
            FROM pg_policies p
            WHERE p.schemaname IN ('tenant', 'public_site', 'engagement')
            ORDER BY p.schemaname, p.tablename, p.policyname
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var key = $"{reader.GetString(0)}.{reader.GetString(1)}";
                if (!tables.TryGetValue(key, out var list))
                {
                    continue;
                }

                var roles = reader.GetString(4).Split(',', StringSplitOptions.RemoveEmptyEntries);
                var qual = reader.IsDBNull(6) ? null : reader.GetString(6);
                var withCheck = reader.IsDBNull(7) ? null : reader.GetString(7);
                list.Add(new PolicyRow(reader.GetString(2), reader.GetString(3), roles, reader.GetString(5), qual, withCheck));
            }
        }

        Assert.Contains("tenant.AuditEntries", tables.Keys);
        Assert.Contains("tenant.Widgets", tables.Keys);
        Assert.Contains("public_site.Posters", tables.Keys);
        Assert.Contains("engagement.Deals", tables.Keys);

        foreach (var (table, policies) in tables)
        {
            Assert.NotEmpty(policies);
            var byName = policies.ToDictionary(policy => policy.Name, StringComparer.Ordinal);
            HashSet<string> standardNames;

            if (table.StartsWith("tenant.", StringComparison.Ordinal))
            {
                Assert.True(byName.TryGetValue("tenant_scope", out var scope), $"{table} is missing tenant_scope.");
                AssertStandardPolicy(table, scope, "ALL", TenantOwnsExpression, TenantOwnsExpression);
                standardNames = ["tenant_scope"];
            }
            else if (table.StartsWith("public_site.", StringComparison.Ordinal))
            {
                Assert.True(byName.TryGetValue("public_read", out var read), $"{table} is missing public_read.");
                AssertStandardPolicy(table, read, "SELECT", PublicReadExpression, null);
                Assert.True(byName.TryGetValue("public_insert", out var insert), $"{table} is missing public_insert.");
                AssertStandardPolicy(table, insert, "INSERT", null, BusinessOwnsExpression);
                Assert.True(byName.TryGetValue("public_update", out var update), $"{table} is missing public_update.");
                AssertStandardPolicy(table, update, "UPDATE", BusinessOwnsExpression, BusinessOwnsExpression);
                Assert.True(byName.TryGetValue("public_delete", out var delete), $"{table} is missing public_delete.");
                AssertStandardPolicy(table, delete, "DELETE", BusinessOwnsExpression, null);
                standardNames = ["public_read", "public_insert", "public_update", "public_delete"];
            }
            else
            {
                Assert.True(byName.TryGetValue("parties_scope", out var parties), $"{table} is missing parties_scope.");
                AssertStandardPolicy(table, parties, "ALL", PartiesExpression, PartiesExpression);
                standardNames = ["parties_scope"];
            }

            foreach (var extra in policies.Where(policy => !standardNames.Contains(policy.Name)))
            {
                AssertDeclaredExtraPolicy(table, extra);
            }
        }
    }

    [Fact]
    public async Task Append_only_tables_block_runtime_updates_and_deletes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(ct);

        await using (var triggerCommand = new NpgsqlCommand("""
            SELECT p.proname, t.tgenabled::text, pg_get_triggerdef(t.oid)
            FROM pg_trigger t
            JOIN pg_proc p ON p.oid = t.tgfoid
            WHERE t.tgrelid = 'tenant."AuditEntries"'::regclass
              AND NOT t.tgisinternal
              AND t.tgname = 'prevent_update_delete'
            """, connection))
        await using (var reader = await triggerCommand.ExecuteReaderAsync(ct))
        {
            Assert.True(await reader.ReadAsync(ct), "tenant.AuditEntries is missing prevent_update_delete trigger.");
            Assert.Equal("prevent_update_delete", reader.GetString(0));
            Assert.NotEqual("D", reader.GetString(1));
            var definition = reader.GetString(2);
            Assert.Contains("BEFORE DELETE OR UPDATE", definition, StringComparison.OrdinalIgnoreCase);
        }

        // SecurityEvents es append-only global: el rol de ejecución puede consultar e insertar, nunca modificar ni borrar.
        Assert.True(await HasRuntimePrivilegeAsync(connection, "platform.\"SecurityEvents\"", "SELECT", ct));
        Assert.True(await HasRuntimePrivilegeAsync(connection, "platform.\"SecurityEvents\"", "INSERT", ct));
        Assert.False(await HasRuntimePrivilegeAsync(connection, "platform.\"SecurityEvents\"", "UPDATE", ct));
        Assert.False(await HasRuntimePrivilegeAsync(connection, "platform.\"SecurityEvents\"", "DELETE", ct));

        // LoginAudits es inmutable ante UPDATE; DELETE existe solo para la purga de retención (ADR 0062).
        Assert.True(await HasRuntimePrivilegeAsync(connection, "identity.\"LoginAudits\"", "SELECT", ct));
        Assert.True(await HasRuntimePrivilegeAsync(connection, "identity.\"LoginAudits\"", "INSERT", ct));
        Assert.False(await HasRuntimePrivilegeAsync(connection, "identity.\"LoginAudits\"", "UPDATE", ct));
    }

    private static void AssertStandardPolicy(
        string table,
        PolicyRow policy,
        string expectedCommand,
        string? expectedQual,
        string? expectedWithCheck)
    {
        Assert.Equal("PERMISSIVE", policy.Permissive);
        Assert.Equal(["public"], policy.Roles);
        Assert.Equal(expectedCommand, policy.Command);

        if (expectedQual is null)
        {
            Assert.Null(policy.Qual);
        }
        else
        {
            Assert.NotNull(policy.Qual);
            Assert.Equal(expectedQual, NormalizeExpression(policy.Qual));
        }

        if (expectedWithCheck is null)
        {
            Assert.Null(policy.WithCheck);
        }
        else
        {
            Assert.NotNull(policy.WithCheck);
            Assert.Equal(expectedWithCheck, NormalizeExpression(policy.WithCheck));
        }

        Assert.False(string.Equals(policy.Qual?.Trim(), "true", StringComparison.OrdinalIgnoreCase),
            $"{table}.{policy.Name} must not use an unrestricted USING (true).");
    }

    private static void AssertDeclaredExtraPolicy(string table, PolicyRow policy)
    {
        var qual = policy.Qual is null ? null : NormalizeExpression(policy.Qual);
        var withCheck = policy.WithCheck is null ? null : NormalizeExpression(policy.WithCheck);

        switch ((table, policy.Name))
        {
            case ("tenant.AuditEntries", "audit_counterpart_insert"):
                Assert.Equal("PERMISSIVE", policy.Permissive);
                Assert.Equal("INSERT", policy.Command);
                Assert.Equal(["mt_app"], policy.Roles);
                Assert.Null(qual);
                Assert.NotNull(withCheck);
                Assert.Contains("app.audit_counterpart_tenant_ids", withCheck, StringComparison.Ordinal);
                break;

            case ("tenant.Members", "account_deletion_personal"):
            case ("tenant.TenantSettings", "account_deletion_personal"):
                Assert.Equal("RESTRICTIVE", policy.Permissive);
                Assert.Equal("DELETE", policy.Command);
                Assert.Equal(["mt_app"], policy.Roles);
                Assert.NotNull(qual);
                Assert.Contains("'personal'", qual, StringComparison.Ordinal);
                Assert.Null(withCheck);
                break;

            case ("tenant.Invitations", "login_invitation_read"):
                Assert.Equal("PERMISSIVE", policy.Permissive);
                Assert.Equal("SELECT", policy.Command);
                Assert.Equal(["mt_app"], policy.Roles);
                Assert.NotNull(qual);
                Assert.Contains("app.login_invitation_destination", qual, StringComparison.Ordinal);
                Assert.Contains("app.login_invitation_keys", qual, StringComparison.Ordinal);
                Assert.Null(withCheck);
                break;

            case ("tenant.Invitations", "login_invitation_accept"):
                Assert.Equal("PERMISSIVE", policy.Permissive);
                Assert.Equal("UPDATE", policy.Command);
                Assert.Equal(["mt_app"], policy.Roles);
                Assert.NotNull(qual);
                Assert.Contains("app.login_invitation_keys", qual, StringComparison.Ordinal);
                Assert.NotNull(withCheck);
                Assert.Contains("app.login_invitation_user_id", withCheck, StringComparison.Ordinal);
                break;

            case ("tenant.Members", "login_invitation_member_read"):
                Assert.Equal("PERMISSIVE", policy.Permissive);
                Assert.Equal("SELECT", policy.Command);
                Assert.Equal(["mt_app"], policy.Roles);
                Assert.NotNull(qual);
                Assert.Contains("app.login_invitation_member_keys", qual, StringComparison.Ordinal);
                Assert.Null(withCheck);
                break;

            case ("tenant.Members", "login_invitation_member_accept"):
                Assert.Equal("PERMISSIVE", policy.Permissive);
                Assert.Equal("UPDATE", policy.Command);
                Assert.Equal(["mt_app"], policy.Roles);
                Assert.NotNull(qual);
                Assert.Contains("app.login_invitation_member_keys", qual, StringComparison.Ordinal);
                Assert.NotNull(withCheck);
                Assert.Contains("app.login_invitation_user_id", withCheck, StringComparison.Ordinal);
                break;

            case ("tenant.FormWorkLeases", "form_work_lease_capacity"):
                // Solo mt_owner a través de la función SECURITY DEFINER count_form_work_leases; nunca mt_app ni public.
                Assert.Equal("PERMISSIVE", policy.Permissive);
                Assert.Equal("SELECT", policy.Command);
                Assert.Equal(["mt_owner"], policy.Roles);
                Assert.Equal("true", qual);
                Assert.Null(withCheck);
                break;

            default:
                Assert.Fail($"Unexpected RLS policy {policy.Name} ({policy.Permissive} {policy.Command}) on {table}.");
                break;
        }
    }

    private static async Task<string[]> LoadProtectedTablesAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        var tables = new List<string>();
        await using var command = new NpgsqlCommand("""
            SELECT n.nspname, c.relname
            FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname IN ('tenant', 'public_site', 'engagement') AND c.relkind IN ('r', 'p')
            ORDER BY n.nspname, c.relname
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            tables.Add($"{reader.GetString(0)}.{reader.GetString(1)}");
        }

        return [.. tables];
    }

    private static async Task<bool> HasRuntimePrivilegeAsync(
        NpgsqlConnection connection, string table, string privilege, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            "SELECT has_table_privilege('mt_app', @table, @privilege)", connection);
        command.Parameters.AddWithValue("table", table);
        command.Parameters.AddWithValue("privilege", privilege);
        return (bool)(await command.ExecuteScalarAsync(ct))!;
    }

    private static string NormalizeExpression(string expression)
    {
        var stripped = expression.Replace("::text", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("(", " ", StringComparison.Ordinal)
            .Replace(")", " ", StringComparison.Ordinal);
        var collapsed = Whitespace().Replace(stripped, " ").Trim().ToLowerInvariant();
        return collapsed
            .Replace(" ::", "::", StringComparison.Ordinal)
            .Replace(" ,", ",", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}
