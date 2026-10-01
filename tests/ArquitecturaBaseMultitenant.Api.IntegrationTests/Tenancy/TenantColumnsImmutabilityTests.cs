using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// Comprueba que las columnas de espacio no puedan cambiar y que la auditoría sea inmutable. Protege ambas
/// reglas mediante restricciones reales de base.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class TenantColumnsImmutabilityTests(ApiFactory factory)
{
    [Fact]
    public async Task Tenant_columns_cannot_change_on_owned_rows()
    {
        var tenants = new TenantFixture();
        var widgetId = Guid.CreateVersion7();
        var posterId = Guid.CreateVersion7();
        var dealId = Guid.CreateVersion7();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var business = await RuntimeRoleConnection.OpenAsync(
            factory.RuntimeConnectionString, tenants.BusinessATenantId, cancellationToken);
        await using var personal = await RuntimeRoleConnection.OpenAsync(
            factory.RuntimeConnectionString, tenants.KevinPersonalTenantId, cancellationToken);

        await using (var transaction = await business.BeginTransactionAsync(cancellationToken))
        {
            await using (var widget = new NpgsqlCommand("""
                INSERT INTO tenant."Widgets" ("TenantId", "Id", "Name", "CreatedAtUtc", "IsDeleted")
                VALUES (@tenant_id, @id, 'private', @created_at_utc, false)
                """, business, transaction))
            {
                widget.Parameters.AddWithValue("tenant_id", tenants.BusinessATenantId);
                widget.Parameters.AddWithValue("id", widgetId);
                widget.Parameters.AddWithValue("created_at_utc", new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc));
                await widget.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var poster = new NpgsqlCommand("""
                INSERT INTO public_site."Posters" ("BusinessTenantId", "Id", "IsPublished")
                VALUES (@tenant_id, @id, false)
                """, business, transaction))
            {
                poster.Parameters.AddWithValue("tenant_id", tenants.BusinessATenantId);
                poster.Parameters.AddWithValue("id", posterId);
                await poster.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        await using (var transaction = await personal.BeginTransactionAsync(cancellationToken))
        {
            await using var deal = new NpgsqlCommand("""
                INSERT INTO engagement."Deals" ("ConsumerTenantId", "BusinessTenantId", "Id", "SharedSummary")
                VALUES (@consumer_id, @business_id, @id, 'shared')
                """, personal, transaction);
            deal.Parameters.AddWithValue("consumer_id", tenants.KevinPersonalTenantId);
            deal.Parameters.AddWithValue("business_id", tenants.BusinessATenantId);
            deal.Parameters.AddWithValue("id", dealId);
            await deal.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await AssertChangeBlockedAsync(business,
            "UPDATE tenant.\"Widgets\" SET \"TenantId\" = @other WHERE \"Id\" = @id",
            widgetId, tenants.BusinessBTenantId, "23514", cancellationToken);
        await AssertChangeBlockedAsync(business,
            "UPDATE public_site.\"Posters\" SET \"BusinessTenantId\" = @other WHERE \"Id\" = @id",
            posterId, tenants.BusinessBTenantId, "23514", cancellationToken);
        await AssertChangeBlockedAsync(personal,
            "UPDATE engagement.\"Deals\" SET \"ConsumerTenantId\" = @other WHERE \"Id\" = @id",
            dealId, tenants.CarlaPersonalTenantId, "23514", cancellationToken);
        await using (var visibility = new NpgsqlCommand(
            "SELECT count(*) FROM engagement.\"Deals\" WHERE \"Id\" = @id", personal))
        {
            visibility.Parameters.AddWithValue("id", dealId);
            Assert.Equal(1L, (long)(await visibility.ExecuteScalarAsync(cancellationToken))!);
        }

        await AssertChangeBlockedAsync(personal,
            "UPDATE engagement.\"Deals\" SET \"BusinessTenantId\" = @other WHERE \"Id\" = @id",
            dealId, tenants.BusinessBTenantId, "23514", cancellationToken);

        var privilegedConnectionString = new NpgsqlConnectionStringBuilder(factory.BootstrapConnectionString)
        {
            Database = "appdb",
        }.ConnectionString;
        await using var privileged = new NpgsqlConnection(privilegedConnectionString);
        await privileged.OpenAsync(cancellationToken);
        await AssertChangeBlockedAsync(privileged,
            "UPDATE tenant.\"Widgets\" SET \"TenantId\" = @other WHERE \"Id\" = @id",
            widgetId, tenants.BusinessBTenantId, "23514", cancellationToken);
        await AssertChangeBlockedAsync(privileged,
            "UPDATE public_site.\"Posters\" SET \"BusinessTenantId\" = @other WHERE \"Id\" = @id",
            posterId, tenants.BusinessBTenantId, "23514", cancellationToken);
        await AssertChangeBlockedAsync(privileged,
            "UPDATE engagement.\"Deals\" SET \"ConsumerTenantId\" = @other WHERE \"Id\" = @id",
            dealId, tenants.CarlaPersonalTenantId, "23514", cancellationToken);
        await AssertChangeBlockedAsync(privileged,
            "UPDATE engagement.\"Deals\" SET \"BusinessTenantId\" = @other WHERE \"Id\" = @id",
            dealId, tenants.BusinessBTenantId, "23514", cancellationToken);
    }

    [Fact]
    public async Task Audit_rows_cannot_be_updated_or_deleted()
    {
        var tenantId = Guid.NewGuid();
        var auditId = Guid.CreateVersion7();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = await RuntimeRoleConnection.OpenAsync(
            factory.RuntimeConnectionString, tenantId, cancellationToken);
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO tenant."AuditEntries"
                    ("TenantId", "Id", "ActorKind", "ActorId", "Action", "EntityType", "EntityId", "Changes", "OccurredAtUtc")
                VALUES (@tenant_id, @id, 'System', NULL, 'Custom', 'RlsTest', @entity_id, '{}'::jsonb, @occurred_at_utc)
                """, connection, transaction);
            insert.Parameters.AddWithValue("tenant_id", tenantId);
            insert.Parameters.AddWithValue("id", auditId);
            insert.Parameters.AddWithValue("entity_id", Guid.CreateVersion7());
            insert.Parameters.AddWithValue("occurred_at_utc", new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc));
            await insert.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await AssertAuditMutationBlockedAsync(connection,
            "UPDATE tenant.\"AuditEntries\" SET \"Changes\" = '{\"changed\":true}'::jsonb WHERE \"Id\" = @id",
            auditId, cancellationToken);
        await AssertAuditMutationBlockedAsync(connection,
            "DELETE FROM tenant.\"AuditEntries\" WHERE \"Id\" = @id",
            auditId, cancellationToken);
    }

    private static async Task AssertChangeBlockedAsync(
        NpgsqlConnection connection, string sql, Guid id, Guid replacementTenantId,
        string expectedSqlState, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("other", replacementTenantId);
        try
        {
            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            Assert.Fail($"Expected SQLSTATE {expectedSqlState}, but the update affected {affected} rows.");
        }
        catch (PostgresException exception)
        {
            Assert.Equal(expectedSqlState, exception.SqlState);
        }
    }

    private static async Task AssertAuditMutationBlockedAsync(
        NpgsqlConnection connection, string sql, Guid id, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        Func<Task> mutate = async () => { await command.ExecuteNonQueryAsync(cancellationToken); };
        var exception = await Assert.ThrowsAsync<PostgresException>(mutate);
        Assert.Equal("23514", exception.SqlState);
    }
}
