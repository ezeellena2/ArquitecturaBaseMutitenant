using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence;

/// <summary>Prepara roles, base ICU y migraciones antes de abrir el servicio en Development o tests.</summary>
public static class DatabaseBootstrapExtensions
{
    public static async Task BootstrapAsync(
        string bootstrapConnectionString,
        string adminConnectionString,
        string runtimeConnectionString,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bootstrapConnectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(adminConnectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeConnectionString);

        var admin = new NpgsqlConnectionStringBuilder(adminConnectionString);
        var runtime = new NpgsqlConnectionStringBuilder(runtimeConnectionString);
        if (admin.Username != "mt_owner" || runtime.Username != "mt_app" ||
            admin.Database != runtime.Database || string.IsNullOrWhiteSpace(admin.Database) ||
            string.IsNullOrWhiteSpace(admin.Password) || string.IsNullOrWhiteSpace(runtime.Password))
        {
            throw new InvalidOperationException("Bootstrap requires mt_owner and mt_app connections to the same database.");
        }

        var database = Identifier(admin.Database);
        await using var postgres = new NpgsqlConnection(bootstrapConnectionString);
        await postgres.OpenAsync(cancellationToken);
        await EnsureRoleAsync(postgres, "mt_owner", admin.Password, cancellationToken);
        await EnsureRoleAsync(postgres, "mt_app", runtime.Password, cancellationToken);

        var existing = await FindDatabaseAsync(postgres, admin.Database, cancellationToken);
        if (existing is not null &&
            (existing.Value.Provider != "i" ||
             !string.Equals(existing.Value.IcuLocale, "es-AR", StringComparison.OrdinalIgnoreCase) ||
             existing.Value.Owner != "mt_owner"))
        {
            var databaseConnection = new NpgsqlConnectionStringBuilder(bootstrapConnectionString)
            {
                Database = admin.Database,
            }.ConnectionString;
            var userTables = await CountUserTablesAsync(databaseConnection, cancellationToken);
            if (userTables != 0)
            {
                throw new InvalidOperationException(
                    "The existing database has user tables and cannot be recreated for ICU es-AR automatically.");
            }

            await ExecuteAsync(postgres, $"DROP DATABASE {database} WITH (FORCE);", cancellationToken);
            existing = null;
        }

        if (existing is null)
        {
            await ExecuteAsync(postgres,
                $"CREATE DATABASE {database} OWNER mt_owner LOCALE_PROVIDER icu ICU_LOCALE 'es-AR' TEMPLATE template0;",
                cancellationToken);
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(adminConnectionString)
            .Options;
        await using (var context = new ApplicationDbContext(options, new TenantContext()))
        {
            await context.Database.MigrateAsync(cancellationToken);
        }

        await RuntimeRoleValidator.ValidateAsync(runtimeConnectionString, cancellationToken);
    }

    private static async Task EnsureRoleAsync(
        NpgsqlConnection connection, string role, string password, CancellationToken cancellationToken)
    {
        await using var lookup = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = @role)", connection);
        lookup.Parameters.AddWithValue("role", role);
        var exists = (bool)(await lookup.ExecuteScalarAsync(cancellationToken))!;

        await using var quote = new NpgsqlCommand("SELECT quote_literal(@password)", connection);
        quote.Parameters.AddWithValue("password", password);
        var quotedPassword = (string)(await quote.ExecuteScalarAsync(cancellationToken))!;
        var verb = exists ? "ALTER" : "CREATE";
        await ExecuteAsync(connection,
            $"{verb} ROLE {Identifier(role)} WITH LOGIN PASSWORD {quotedPassword} NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION;",
            cancellationToken);
    }

    private static async Task<(string Owner, string Provider, string? IcuLocale)?> FindDatabaseAsync(
        NpgsqlConnection connection, string database, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT pg_get_userbyid(datdba), datlocprovider::text, datlocale
            FROM pg_database WHERE datname = @database
            """, connection);
        command.Parameters.AddWithValue("database", database);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return (reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    private static async Task<long> CountUserTablesAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind IN ('r', 'p') AND n.nspname NOT IN ('pg_catalog', 'information_schema')
              AND n.nspname NOT LIKE 'pg_toast%'
            """, connection);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string Identifier(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 63 ||
            !Regex.IsMatch(value, "^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("Invalid database identifier.", nameof(value));
        }

        return $"\"{value}\"";
    }
}
