using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// Comprueba que el rol de ejecución de la aplicación no tenga privilegios de dueño ni bypass de RLS.
/// Protege el aislamiento impuesto por PostgreSQL.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class RuntimeRoleTests(ApiFactory factory)
{
    [Fact]
    public async Task Runtime_role_is_not_privileged()
    {
        await using var connection = new NpgsqlConnection(factory.RuntimeConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT current_user, r.rolsuper, r.rolbypassrls, r.rolcreatedb, r.rolcreaterole,
                   d.datdba = r.oid, pg_has_role(current_user, 'mt_owner', 'MEMBER'),
                   has_schema_privilege(current_user, 'tenant', 'CREATE'),
                   (SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                    WHERE n.nspname IN ('tenant', 'public_site', 'engagement') AND c.relowner = r.oid)
            FROM pg_roles r CROSS JOIN pg_database d
            WHERE r.rolname = current_user AND d.datname = current_database()
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal("mt_app", reader.GetString(0));
        for (var column = 1; column <= 7; column++)
        {
            Assert.False(reader.GetBoolean(column));
        }

        Assert.Equal(0L, reader.GetInt64(8));
    }
}
