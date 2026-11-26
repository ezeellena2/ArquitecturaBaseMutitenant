using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// Comprueba que el rol de ejecución de la aplicación no tenga privilegios de dueño ni bypass de RLS, y que no pueda crear
/// esquemas ni crear o ser dueño de nada en ningún esquema de la base, no solo en los de datos de organizaciones (ADR 0068:
/// <c>mt_app</c> no crea ni altera tablas). Protege el aislamiento impuesto por PostgreSQL.
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
                   has_database_privilege(current_user, d.oid, 'CREATE'),
                   (SELECT coalesce(string_agg(n.nspname, ', ' ORDER BY n.nspname), '') FROM pg_namespace n
                    WHERE n.nspname <> 'information_schema' AND n.nspname NOT LIKE 'pg\_%'
                      AND has_schema_privilege(current_user, n.oid, 'CREATE')),
                   (SELECT coalesce(string_agg(n.nspname || '.' || c.relname, ', ' ORDER BY n.nspname, c.relname), '')
                    FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                    WHERE n.nspname <> 'information_schema' AND n.nspname NOT LIKE 'pg\_%' AND c.relowner = r.oid)
            FROM pg_roles r CROSS JOIN pg_database d
            WHERE r.rolname = current_user AND d.datname = current_database()
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal("mt_app", reader.GetString(0));
        // Sin CREATE en la base tampoco puede crear un esquema propio donde armar tablas.
        for (var column = 1; column <= 7; column++)
        {
            Assert.False(reader.GetBoolean(column));
        }

        // Los esquemas donde puede crear y las tablas, vistas o secuencias de las que es dueño, en cualquier esquema.
        Assert.Equal(string.Empty, reader.GetString(8));
        Assert.Equal(string.Empty, reader.GetString(9));
    }
}
