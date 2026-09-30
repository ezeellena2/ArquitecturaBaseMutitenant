using Npgsql;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;

/// <summary>Falla al arrancar si la conexión operativa puede saltear RLS o usa otra collation.</summary>
public static class RuntimeRoleValidator
{
    public static async Task ValidateAsync(string connectionString, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT r.rolname, r.rolsuper, r.rolbypassrls, r.rolcreatedb, r.rolcreaterole,
                   d.datdba = r.oid, d.datlocprovider::text, d.datlocale,
                   pg_has_role(current_user, 'mt_owner', 'MEMBER'),
                   EXISTS (SELECT 1 FROM pg_roles granted
                           WHERE granted.oid <> r.oid
                             AND (granted.rolsuper OR granted.rolbypassrls)
                             AND pg_has_role(r.oid, granted.oid, 'MEMBER')),
                   EXISTS (SELECT 1 FROM pg_namespace n
                           WHERE n.nspname IN ('platform', 'identity', 'tenant', 'public_site', 'engagement')
                             AND has_schema_privilege(n.oid, 'CREATE')),
                   EXISTS (SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                           WHERE n.nspname IN ('platform', 'identity', 'tenant', 'public_site', 'engagement')
                             AND c.relowner = r.oid)
            FROM pg_roles r CROSS JOIN pg_database d
            WHERE r.rolname = current_user AND d.datname = current_database()
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("The runtime database role or database was not found.");
        }

        var valid = reader.GetString(0) == "mt_app"
            && !reader.GetBoolean(1) // superuser
            && !reader.GetBoolean(2) // BYPASSRLS
            && !reader.GetBoolean(3) // CREATEDB
            && !reader.GetBoolean(4) // CREATEROLE
            && !reader.GetBoolean(5) // database owner
            && reader.GetString(6) == "i"
            && !reader.IsDBNull(7)
            && reader.GetString(7).Equals(DatabaseBootstrapExtensions.DatabaseIcuLocale,
                StringComparison.OrdinalIgnoreCase)
            && !reader.GetBoolean(8) // member of mt_owner
            && !reader.GetBoolean(9) // member of a superuser or BYPASSRLS role
            && !reader.GetBoolean(10) // CREATE in an application schema
            && !reader.GetBoolean(11); // owns a relation in an application schema

        if (!valid)
        {
            throw new InvalidOperationException("The runtime role must be unprivileged and the database must use ICU es-AR.");
        }
    }
}
