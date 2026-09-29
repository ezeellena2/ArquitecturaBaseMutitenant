using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

[Collection(ApiTestGroup.Name)]
public sealed class BootstrapTests(ApiFactory factory)
{
    [Fact]
    public async Task Database_is_created_with_icu_es_ar_and_five_schemas()
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT d.datlocprovider::text, d.datlocale,
                   (SELECT count(*) FROM pg_namespace WHERE nspname IN
                    ('platform', 'identity', 'tenant', 'public_site', 'engagement'))
            FROM pg_database d WHERE d.datname = current_database()
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal("i", reader.GetString(0));
        Assert.Equal("es-AR", reader.GetString(1));
        Assert.Equal(5, reader.GetInt64(2));
    }

    [Fact]
    public async Task Bootstrap_separates_owner_and_unprivileged_runtime_role()
    {
        await using var connection = new NpgsqlConnection(factory.RuntimeConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT current_user, r.rolsuper, r.rolbypassrls, r.rolcreatedb, r.rolcreaterole,
                   d.datdba = r.oid
            FROM pg_roles r CROSS JOIN pg_database d
            WHERE r.rolname = current_user AND d.datname = current_database()
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal("mt_app", reader.GetString(0));
        for (var index = 1; index <= 5; index++)
        {
            Assert.False(reader.GetBoolean(index));
        }
    }

    [Fact]
    public async Task Migration_is_applied_by_owner_before_runtime_connections()
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT count(*) FROM public."__EFMigrationsHistory"
            """, connection);

        var count = (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
        Assert.True(count > 0);
    }

    [Fact]
    public async Task Empty_legacy_database_is_recreated_with_icu_locale()
    {
        const string database = "legacy_empty";
        await CreateLegacyDatabaseAsync(database);

        await DatabaseBootstrapExtensions.BootstrapAsync(
            factory.BootstrapConnectionString,
            ForDatabase(factory.AdminConnectionString, database),
            ForDatabase(factory.RuntimeConnectionString, database),
            TestContext.Current.CancellationToken);

        await using var connection = new NpgsqlConnection(ForDatabase(factory.AdminConnectionString, database));
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT datlocale FROM pg_database WHERE datname = current_database()", connection);
        Assert.Equal("es-AR", await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Nonempty_legacy_database_is_preserved_and_bootstrap_stops()
    {
        const string database = "legacy_nonempty";
        await CreateLegacyDatabaseAsync(database);
        await using (var legacy = new NpgsqlConnection(ForDatabase(factory.BootstrapConnectionString, database)))
        {
            await legacy.OpenAsync(TestContext.Current.CancellationToken);
            await using var create = new NpgsqlCommand("CREATE TABLE public.legacy_data (id int PRIMARY KEY)", legacy);
            await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DatabaseBootstrapExtensions.BootstrapAsync(
                factory.BootstrapConnectionString,
                ForDatabase(factory.AdminConnectionString, database),
                ForDatabase(factory.RuntimeConnectionString, database),
                TestContext.Current.CancellationToken));
        Assert.Contains("user tables", error.Message, StringComparison.Ordinal);

        await using var connection = new NpgsqlConnection(ForDatabase(factory.BootstrapConnectionString, database));
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("SELECT to_regclass('public.legacy_data') IS NOT NULL", connection);
        Assert.True((bool)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!);
    }

    [Fact]
    public async Task Runtime_role_validator_rejects_superuser()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RuntimeRoleValidator.ValidateAsync(
                factory.BootstrapConnectionString,
                TestContext.Current.CancellationToken));
    }

    private async Task CreateLegacyDatabaseAsync(string database)
    {
        await using var connection = new NpgsqlConnection(factory.BootstrapConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand($"CREATE DATABASE {database}", connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static string ForDatabase(string connectionString, string database)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { Database = database };
        return builder.ConnectionString;
    }
}
