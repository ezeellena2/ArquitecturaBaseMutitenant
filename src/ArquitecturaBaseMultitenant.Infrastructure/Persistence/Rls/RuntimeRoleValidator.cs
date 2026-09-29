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
                   d.datdba = r.oid, d.datlocprovider::text, d.datlocale
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
            && reader.GetString(7).Equals("es-AR", StringComparison.OrdinalIgnoreCase);

        if (!valid)
        {
            throw new InvalidOperationException("The runtime role must be unprivileged and the database must use ICU es-AR.");
        }
    }
}
