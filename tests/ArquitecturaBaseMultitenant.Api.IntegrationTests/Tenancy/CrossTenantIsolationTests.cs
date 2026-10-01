using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// Comprueba que las vistas de acceso no mezclen datos entre espacios. Protege el aislamiento durante
/// lecturas desde distintos contextos.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class CrossTenantIsolationTests(ApiFactory factory)
{
    [Fact]
    public async Task Access_views_do_not_mix()
    {
        var tenants = new TenantFixture();
        var businessWidgetId = Guid.CreateVersion7();
        var personalWidgetId = Guid.CreateVersion7();
        var cancellationToken = TestContext.Current.CancellationToken;

        await InsertWidgetAsync(tenants.BusinessATenantId, businessWidgetId, cancellationToken);
        await InsertWidgetAsync(tenants.KevinPersonalTenantId, personalWidgetId, cancellationToken);

        await using var business = await RuntimeRoleConnection.OpenAsync(
            factory.RuntimeConnectionString, tenants.BusinessATenantId, cancellationToken);
        await using var personal = await RuntimeRoleConnection.OpenAsync(
            factory.RuntimeConnectionString, tenants.KevinPersonalTenantId, cancellationToken);
        await using var otherBusiness = await RuntimeRoleConnection.OpenAsync(
            factory.RuntimeConnectionString, tenants.BusinessBTenantId, cancellationToken);

        Assert.Equal([businessWidgetId], await VisibleIdsAsync(business, businessWidgetId, personalWidgetId, cancellationToken));
        Assert.Equal([personalWidgetId], await VisibleIdsAsync(personal, businessWidgetId, personalWidgetId, cancellationToken));
        Assert.Empty(await VisibleIdsAsync(otherBusiness, businessWidgetId, personalWidgetId, cancellationToken));
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

    private static async Task<Guid[]> VisibleIdsAsync(
        NpgsqlConnection connection, Guid firstId, Guid secondId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT "Id" FROM tenant."Widgets" WHERE "Id" IN (@first_id, @second_id)
            """, connection);
        command.Parameters.AddWithValue("first_id", firstId);
        command.Parameters.AddWithValue("second_id", secondId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var ids = new List<Guid>();
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetGuid(0));
        }

        return [.. ids];
    }
}
