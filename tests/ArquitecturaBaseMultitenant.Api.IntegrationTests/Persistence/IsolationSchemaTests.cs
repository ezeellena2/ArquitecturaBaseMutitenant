using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

/// <summary>
/// Comprueba el esquema sintético usado para probar las tres clases de datos. Exige claves por espacio, RLS
/// forzado y políticas equivalentes a las productivas.
/// </summary>
public sealed class IsolationSchemaTests
{
    [Fact]
    public void Derived_model_uses_tenant_first_keys_for_test_tables()
    {
        var options = new DbContextOptionsBuilder<IsolationApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=isolation_model_test")
            .ReplaceService<IModelCustomizer, IsolationModelCustomizer>()
            .Options;
        using var context = new IsolationApplicationDbContext(options, new EmptyTenantContext());

        AssertKey<Widget>(context, "tenant", "Widgets", "TenantId", "Id");
        AssertKey<Poster>(context, "public_site", "Posters", "BusinessTenantId", "Id");
        AssertKey<Deal>(context, "engagement", "Deals", "ConsumerTenantId", "BusinessTenantId", "Id");
    }

    private static void AssertKey<TEntity>(DbContext context, string schema, string table, params string[] properties)
    {
        var entity = context.Model.FindEntityType(typeof(TEntity));
        Assert.NotNull(entity);
        Assert.Equal(schema, entity.GetSchema());
        Assert.Equal(table, entity.GetTableName());
        Assert.Equal(properties, entity.FindPrimaryKey()!.Properties.Select(property => property.Name));
    }

    private sealed class EmptyTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No active tenant.");
    }
}

[Collection(ApiTestGroup.Name)]
public sealed class IsolationSchemaDatabaseTests(ApiFactory factory)
{
    [Fact]
    public async Task Test_tables_have_forced_rls_and_expected_policies()
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT n.nspname || '.' || c.relname, c.relrowsecurity, c.relforcerowsecurity,
                   (SELECT count(*) FROM pg_policies p
                    WHERE p.schemaname = n.nspname AND p.tablename = c.relname)
            FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE (n.nspname, c.relname) IN
                  (('tenant', 'Widgets'), ('public_site', 'Posters'), ('engagement', 'Deals'))
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var tables = new Dictionary<string, (bool Enabled, bool Forced, long PolicyCount)>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            tables.Add(reader.GetString(0), (reader.GetBoolean(1), reader.GetBoolean(2), reader.GetInt64(3)));
        }

        Assert.Equal(3, tables.Count);
        Assert.Equal((true, true, 1L), tables["tenant.Widgets"]);
        Assert.Equal((true, true, 4L), tables["public_site.Posters"]);
        Assert.Equal((true, true, 1L), tables["engagement.Deals"]);
    }

    [Fact]
    public async Task Runtime_role_can_read_all_three_test_tables()
    {
        await using var connection = await RuntimeRoleConnection.OpenAsync(
            factory.RuntimeConnectionString, Guid.NewGuid(), TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT (SELECT count(*) FROM tenant."Widgets"),
                   (SELECT count(*) FROM public_site."Posters"),
                   (SELECT count(*) FROM engagement."Deals")
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(3, reader.FieldCount);
        Assert.Equal(0, reader.GetInt64(0));
        Assert.Equal(0, reader.GetInt64(2));
    }
}
