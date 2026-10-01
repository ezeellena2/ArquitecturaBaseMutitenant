using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

/// <summary>
/// Comprueba creación de base, esquemas y collation ICU es-AR. Exige separar el dueño de la base del rol
/// limitado que ejecuta la aplicación.
/// </summary>
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

    [Fact]
    public async Task Runtime_role_validator_rejects_owner_membership()
    {
        await WithBootstrapChangeAsync("GRANT mt_owner TO mt_app", "REVOKE mt_owner FROM mt_app", () =>
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                RuntimeRoleValidator.ValidateAsync(factory.RuntimeConnectionString,
                    TestContext.Current.CancellationToken)));
    }

    [Theory]
    [InlineData("SUPERUSER", "runtime_super_probe")]
    [InlineData("BYPASSRLS", "runtime_bypass_probe")]
    public async Task Runtime_role_validator_rejects_privileged_role_membership(string privilege, string role)
    {
        await WithBootstrapChangeAsync($"CREATE ROLE {role} {privilege}; GRANT {role} TO mt_app",
            $"REVOKE {role} FROM mt_app; DROP ROLE {role}", () =>
                Assert.ThrowsAsync<InvalidOperationException>(() =>
                    RuntimeRoleValidator.ValidateAsync(factory.RuntimeConnectionString,
                        TestContext.Current.CancellationToken)));
    }

    [Theory]
    [InlineData("platform")]
    [InlineData("identity")]
    [InlineData("tenant")]
    [InlineData("public_site")]
    [InlineData("engagement")]
    public async Task Runtime_role_validator_rejects_schema_create(string schema)
    {
        await WithBootstrapChangeAsync($"GRANT CREATE ON SCHEMA {schema} TO mt_app",
            $"REVOKE CREATE ON SCHEMA {schema} FROM mt_app", () =>
                Assert.ThrowsAsync<InvalidOperationException>(() =>
                    RuntimeRoleValidator.ValidateAsync(factory.RuntimeConnectionString,
                        TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task Runtime_role_validator_rejects_runtime_owned_relation()
    {
        await WithBootstrapChangeAsync("""
            CREATE TABLE tenant."RuntimeRoleProbe" ("Id" integer);
            ALTER TABLE tenant."RuntimeRoleProbe" OWNER TO mt_app;
            """, "DROP TABLE tenant.\"RuntimeRoleProbe\"", () =>
                Assert.ThrowsAsync<InvalidOperationException>(() =>
                    RuntimeRoleValidator.ValidateAsync(factory.RuntimeConnectionString,
                        TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task Bootstrap_removes_runtime_role_memberships()
    {
        await WithBootstrapChangeAsync("""
            CREATE ROLE runtime_membership_probe;
            GRANT mt_owner, runtime_membership_probe TO mt_app;
            """, """
            REVOKE mt_owner, runtime_membership_probe FROM mt_app;
            DROP ROLE runtime_membership_probe;
            """, async () =>
        {
            await DatabaseBootstrapExtensions.BootstrapAsync(factory.BootstrapConnectionString,
                factory.AdminConnectionString, factory.RuntimeConnectionString,
                TestContext.Current.CancellationToken);

            await using var connection = new NpgsqlConnection(factory.RuntimeConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = new NpgsqlCommand("""
                SELECT pg_has_role(current_user, 'mt_owner', 'MEMBER'),
                       pg_has_role(current_user, 'runtime_membership_probe', 'MEMBER')
                """, connection);
            await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
            Assert.False(reader.GetBoolean(0));
            Assert.False(reader.GetBoolean(1));
        });
    }

    private async Task WithBootstrapChangeAsync(string setupSql, string cleanupSql, Func<Task> assertion)
    {
        await using var connection = new NpgsqlConnection(ForDatabase(factory.BootstrapConnectionString, "appdb"));
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using (var setup = new NpgsqlCommand(setupSql, connection))
        {
            await setup.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        try
        {
            await assertion();
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand(cleanupSql, connection);
            await cleanup.ExecuteNonQueryAsync(CancellationToken.None);
        }
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
