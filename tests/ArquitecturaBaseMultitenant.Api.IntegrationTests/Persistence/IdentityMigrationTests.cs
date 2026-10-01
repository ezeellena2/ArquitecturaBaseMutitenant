using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

/// <summary>
/// Comprueba el SQL de las migraciones de identidad, legales y mensajería. Exige tablas, RLS y permisos
/// runtime acordes al modelo.
/// </summary>
public sealed class IdentityMigrationTests
{
    [Fact]
    public void Forward_migration_creates_identity_legal_outbox_and_tenant_tables()
    {
        using var context = CreateContext();
        var script = context.GetService<IMigrator>().GenerateScript();

        foreach (var table in new[] { "AspNetUsers", "AspNetUserClaims", "AspNetUserLogins", "AspNetUserTokens",
                     "LoginMethods", "LoginCodes", "LoginAudits", "LegalAcceptances" })
        {
            Assert.Contains($"CREATE TABLE identity.\"{table}\"", script, StringComparison.Ordinal);
        }

        foreach (var table in new[] { "Tenants", "PlatformSettings", "SecurityEvents", "OutboxMessages",
                     "LegalDocuments", "LegalDocumentContents" })
        {
            Assert.Contains($"CREATE TABLE platform.\"{table}\"", script, StringComparison.Ordinal);
        }

        Assert.Contains("CREATE TABLE tenant.\"Members\"", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE tenant.\"TenantSettings\"", script, StringComparison.Ordinal);
        Assert.Contains("IX_LoginMethods_Type_Value", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Followup_migration_forces_rls_and_grants_only_required_runtime_access()
    {
        using var context = CreateContext();
        var script = context.GetService<IMigrator>().GenerateScript();

        Assert.Contains("ALTER TABLE \"tenant\".\"Members\" FORCE ROW LEVEL SECURITY;", script,
            StringComparison.Ordinal);
        Assert.Contains("ALTER TABLE \"tenant\".\"TenantSettings\" FORCE ROW LEVEL SECURITY;", script,
            StringComparison.Ordinal);
        Assert.Contains("GRANT SELECT, INSERT, UPDATE ON TABLE tenant.\"Members\" TO mt_app;", script,
            StringComparison.Ordinal);
        Assert.Contains("GRANT SELECT, INSERT, UPDATE ON TABLE identity.\"AspNetUsers\" TO mt_app;", script,
            StringComparison.Ordinal);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=identity_migration_test").Options;
        return new ApplicationDbContext(options, new EmptyTenantContext());
    }

    private sealed class EmptyTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No active tenant.");
    }
}

[Collection(ApiTestGroup.Name)]
public sealed class IdentityMigrationDatabaseTests(ApiFactory factory)
{
    [Theory]
    [InlineData("Members")]
    [InlineData("TenantSettings")]
    public async Task Tenant_identity_tables_force_rls_in_postgres(string table)
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT c.relrowsecurity, c.relforcerowsecurity
            FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'tenant' AND c.relname = @table
            """, connection);
        command.Parameters.AddWithValue("table", table);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.True(reader.GetBoolean(0));
        Assert.True(reader.GetBoolean(1));
    }

    [Fact]
    public async Task Runtime_role_can_use_new_tables_without_owning_them()
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT has_table_privilege('mt_app', 'identity."AspNetUsers"', 'SELECT'),
                   has_table_privilege('mt_app', 'tenant."Members"', 'INSERT'),
                   has_table_privilege('mt_app', 'platform."OutboxMessages"', 'UPDATE'),
                   has_table_privilege('mt_app', 'platform."SecurityEvents"', 'DELETE')
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.True(reader.GetBoolean(0));
        Assert.True(reader.GetBoolean(1));
        Assert.True(reader.GetBoolean(2));
        Assert.False(reader.GetBoolean(3));
    }
}
