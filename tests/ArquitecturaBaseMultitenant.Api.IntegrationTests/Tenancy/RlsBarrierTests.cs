using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// Comprueba el aislamiento real de PostgreSQL aun cuando se ignoren filtros de EF. Protege la segunda
/// barrera contra lecturas entre organizaciones.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class RlsBarrierTests(ApiFactory factory)
{
    [Fact]
    public async Task Rls_blocks_cross_tenant_even_with_filters_ignored()
    {
        var tenants = new TenantFixture();
        var ownId = Guid.CreateVersion7();
        var foreignId = Guid.CreateVersion7();
        var cancellationToken = TestContext.Current.CancellationToken;
        await InsertWidgetAsync(tenants.BusinessATenantId, ownId, cancellationToken);
        await InsertWidgetAsync(tenants.BusinessBTenantId, foreignId, cancellationToken);

        await using var connection = await RuntimeRoleConnection.OpenAsync(
            factory.RuntimeConnectionString, tenants.BusinessATenantId, cancellationToken);
        var options = new DbContextOptionsBuilder<IsolationApplicationDbContext>()
            .UseNpgsql(connection)
            .ReplaceService<IModelCustomizer, IsolationModelCustomizer>()
            .Options;
        await using (var context = new IsolationApplicationDbContext(options, new TestTenantContext(tenants.BusinessATenantId)))
        {
            var visibleIds = await context.Set<Widget>()
                .IgnoreQueryFilters()
                .Where(widget => widget.Id == ownId || widget.Id == foreignId)
                .Select(widget => widget.Id)
                .ToArrayAsync(cancellationToken);
            Assert.Equal([ownId], visibleIds);
        }

        await using (var read = new NpgsqlCommand(
            "SELECT count(*) FROM tenant.\"Widgets\" WHERE \"Id\" = @id", connection))
        {
            read.Parameters.AddWithValue("id", foreignId);
            Assert.Equal(0L, (long)(await read.ExecuteScalarAsync(cancellationToken))!);
        }

        Assert.Equal(0, await MutateForeignWidgetAsync(connection,
            "UPDATE tenant.\"Widgets\" SET \"Name\" = 'changed' WHERE \"Id\" = @id",
            foreignId, cancellationToken));
        Assert.Equal(0, await MutateForeignWidgetAsync(connection,
            "DELETE FROM tenant.\"Widgets\" WHERE \"Id\" = @id",
            foreignId, cancellationToken));

        await using var insert = new NpgsqlCommand("""
            INSERT INTO tenant."Widgets" ("TenantId", "Id", "Name", "CreatedAtUtc", "IsDeleted")
            VALUES (@tenant_id, @id, 'forged', @created_at_utc, false)
            """, connection);
        insert.Parameters.AddWithValue("tenant_id", tenants.BusinessBTenantId);
        insert.Parameters.AddWithValue("id", Guid.CreateVersion7());
        insert.Parameters.AddWithValue("created_at_utc", new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc));
        Func<Task> forge = async () => { await insert.ExecuteNonQueryAsync(cancellationToken); };
        var exception = await Assert.ThrowsAsync<PostgresException>(forge);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }

    private async Task InsertWidgetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await RuntimeRoleConnection.OpenAsync(
            factory.RuntimeConnectionString, tenantId, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            INSERT INTO tenant."Widgets" ("TenantId", "Id", "Name", "CreatedAtUtc", "IsDeleted")
            VALUES (@tenant_id, @id, 'private', @created_at_utc, false)
            """, connection, transaction);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("created_at_utc", new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc));
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<int> MutateForeignWidgetAsync(
        NpgsqlConnection connection, string sql, Guid id, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed class TestTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => tenantId;
    }
}
