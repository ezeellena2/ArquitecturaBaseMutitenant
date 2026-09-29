using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

public sealed class OpenIddictMigrationScriptTests
{
    [Fact]
    public void Forward_migration_creates_four_platform_tables_and_runtime_grants()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=oidc_migration_test");
        options.UseOpenIddict<Guid>();
        using var context = new ApplicationDbContext(options.Options, new EmptyTenantContext());
        var script = context.GetService<IMigrator>().GenerateScript();

        foreach (var table in new[]
                 { "OpenIddictApplications", "OpenIddictAuthorizations", "OpenIddictScopes", "OpenIddictTokens" })
        {
            Assert.Contains($"CREATE TABLE platform.\"{table}\"", script, StringComparison.Ordinal);
            Assert.Contains($"GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE platform.\"{table}\" TO mt_app;",
                script, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("CREATE TABLE identity.\"OpenIddict", script, StringComparison.Ordinal);
    }

    private sealed class EmptyTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No active tenant.");
    }
}

[Collection(ApiTestGroup.Name)]
public sealed class OpenIddictMigrationTests(ApiFactory factory)
{
    [Fact]
    public async Task Runtime_role_can_use_platform_oidc_tables_after_second_bootstrap()
    {
        await DatabaseBootstrapExtensions.BootstrapAsync(
            factory.BootstrapConnectionString,
            factory.AdminConnectionString,
            factory.RuntimeConnectionString,
            TestContext.Current.CancellationToken);

        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT count(*) = 4,
                   bool_and(has_table_privilege('mt_app', format('platform.%I', tablename), 'SELECT, INSERT, UPDATE, DELETE'))
            FROM pg_tables
            WHERE schemaname = 'platform' AND tablename LIKE 'OpenIddict%'
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.True(reader.GetBoolean(0));
        Assert.True(reader.GetBoolean(1));
    }
}
