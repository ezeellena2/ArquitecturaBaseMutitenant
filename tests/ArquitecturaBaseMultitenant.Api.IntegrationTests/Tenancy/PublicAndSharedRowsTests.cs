using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;
using ArquitecturaBaseMultitenant.Domain.Common;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

[Collection(ApiTestGroup.Name)]
public sealed class PublicAndSharedRowsTests(ApiFactory factory)
{
    [Fact]
    public async Task Public_rows_visible_only_when_published()
    {
        var tenants = new TenantFixture();
        var draftA = Guid.CreateVersion7();
        var publishedA = Guid.CreateVersion7();
        var draftB = Guid.CreateVersion7();
        var cancellationToken = TestContext.Current.CancellationToken;
        await InsertPosterAsync(tenants.BusinessATenantId, draftA, false, cancellationToken);
        await InsertPosterAsync(tenants.BusinessATenantId, publishedA, true, cancellationToken);
        await InsertPosterAsync(tenants.BusinessBTenantId, draftB, false, cancellationToken);

        await using var visitor = await RuntimeRoleConnection.OpenAsync(
            factory.RuntimeConnectionString, null, cancellationToken);
        await using var businessA = await RuntimeRoleConnection.OpenAsync(
            factory.RuntimeConnectionString, tenants.BusinessATenantId, cancellationToken);
        await using var businessB = await RuntimeRoleConnection.OpenAsync(
            factory.RuntimeConnectionString, tenants.BusinessBTenantId, cancellationToken);
        var ids = new[] { draftA, publishedA, draftB };

        Assert.Equal([publishedA], await VisiblePosterIdsAsync(visitor, ids, cancellationToken));
        var seenByA = await VisiblePosterIdsAsync(businessA, ids, cancellationToken);
        Assert.Equal(2, seenByA.Length);
        Assert.Contains(draftA, seenByA);
        Assert.Contains(publishedA, seenByA);
        var seenByB = await VisiblePosterIdsAsync(businessB, ids, cancellationToken);
        Assert.Equal(2, seenByB.Length);
        Assert.Contains(draftB, seenByB);
        Assert.Contains(publishedA, seenByB);
        Assert.Equal(0, await ExecuteForIdAsync(businessB,
            "UPDATE public_site.\"Posters\" SET \"IsPublished\" = true WHERE \"Id\" = @id",
            draftA, cancellationToken));
        Assert.Equal(0, await ExecuteForIdAsync(businessB,
            "DELETE FROM public_site.\"Posters\" WHERE \"Id\" = @id",
            draftA, cancellationToken));
        await using (var forged = new NpgsqlCommand("""
            INSERT INTO public_site."Posters" ("BusinessTenantId", "Id", "IsPublished")
            VALUES (@tenant_id, @id, false)
            """, businessB))
        {
            forged.Parameters.AddWithValue("tenant_id", tenants.BusinessATenantId);
            forged.Parameters.AddWithValue("id", Guid.CreateVersion7());
            Func<Task> insert = async () => { await forged.ExecuteNonQueryAsync(cancellationToken); };
            var exception = await Assert.ThrowsAsync<PostgresException>(insert);
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
        }

        Assert.Equal([publishedA], await VisiblePosterIdsAsync(visitor, ids, cancellationToken));
    }

    [Fact]
    public async Task Shared_rows_visible_only_to_parties()
    {
        var tenants = new TenantFixture();
        var dealId = Guid.CreateVersion7();
        var cancellationToken = TestContext.Current.CancellationToken;
        await InsertDealAsync(tenants.KevinPersonalTenantId, tenants.BusinessATenantId,
            dealId, "Shared order summary", cancellationToken);

        await using var kevin = await RuntimeRoleConnection.OpenAsync(
            factory.RuntimeConnectionString, tenants.KevinPersonalTenantId, cancellationToken);
        await using var businessA = await RuntimeRoleConnection.OpenAsync(
            factory.RuntimeConnectionString, tenants.BusinessATenantId, cancellationToken);
        await using var carla = await RuntimeRoleConnection.OpenAsync(
            factory.RuntimeConnectionString, tenants.CarlaPersonalTenantId, cancellationToken);
        await using var businessB = await RuntimeRoleConnection.OpenAsync(
            factory.RuntimeConnectionString, tenants.BusinessBTenantId, cancellationToken);

        Assert.Equal(1L, await CountDealAsync(kevin, dealId, cancellationToken));
        Assert.Equal(1L, await CountDealAsync(businessA, dealId, cancellationToken));
        Assert.Equal(0L, await CountDealAsync(carla, dealId, cancellationToken));
        Assert.Equal(0L, await CountDealAsync(businessB, dealId, cancellationToken));
        Assert.Equal(0, await ExecuteForIdAsync(carla,
            "UPDATE engagement.\"Deals\" SET \"SharedSummary\" = 'tampered' WHERE \"Id\" = @id",
            dealId, cancellationToken));
        Assert.Equal(0, await ExecuteForIdAsync(businessB,
            "DELETE FROM engagement.\"Deals\" WHERE \"Id\" = @id",
            dealId, cancellationToken));
        await using (var forged = new NpgsqlCommand("""
            INSERT INTO engagement."Deals" ("ConsumerTenantId", "BusinessTenantId", "Id", "SharedSummary")
            VALUES (@consumer_id, @business_id, @id, 'forged')
            """, businessB))
        {
            forged.Parameters.AddWithValue("consumer_id", tenants.KevinPersonalTenantId);
            forged.Parameters.AddWithValue("business_id", tenants.BusinessATenantId);
            forged.Parameters.AddWithValue("id", Guid.CreateVersion7());
            Func<Task> insert = async () => { await forged.ExecuteNonQueryAsync(cancellationToken); };
            var exception = await Assert.ThrowsAsync<PostgresException>(insert);
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
        }

        Assert.Equal(1L, await CountDealAsync(businessA, dealId, cancellationToken));
        await using (var summary = new NpgsqlCommand(
            "SELECT \"SharedSummary\" FROM engagement.\"Deals\" WHERE \"Id\" = @id", businessA))
        {
            summary.Parameters.AddWithValue("id", dealId);
            Assert.Equal("Shared order summary", (string)(await summary.ExecuteScalarAsync(cancellationToken))!);
        }

        var deal = new Deal(tenants.KevinPersonalTenantId, tenants.BusinessATenantId, "Shared order summary");
        Assert.True(PartyPolicy.Require(deal, Party.Business, tenants.BusinessATenantId, Party.Business));
        Assert.False(PartyPolicy.Require(deal, Party.Consumer, tenants.KevinPersonalTenantId, Party.Business));
        Assert.False(PartyPolicy.Require(deal, Party.Business, tenants.BusinessBTenantId, Party.Business));
    }

    [Fact]
    public async Task Business_sees_only_what_was_shared()
    {
        var tenants = new TenantFixture();
        var privateWidgetId = Guid.CreateVersion7();
        var dealId = Guid.CreateVersion7();
        var cancellationToken = TestContext.Current.CancellationToken;
        await InsertWidgetAsync(tenants.KevinPersonalTenantId, privateWidgetId, cancellationToken);
        await InsertDealAsync(tenants.KevinPersonalTenantId, tenants.BusinessATenantId,
            dealId, "Copied name for business", cancellationToken);

        await using var business = await RuntimeRoleConnection.OpenAsync(
            factory.RuntimeConnectionString, tenants.BusinessATenantId, cancellationToken);
        await using (var privateRead = new NpgsqlCommand(
            "SELECT count(*) FROM tenant.\"Widgets\" WHERE \"Id\" = @id", business))
        {
            privateRead.Parameters.AddWithValue("id", privateWidgetId);
            Assert.Equal(0L, (long)(await privateRead.ExecuteScalarAsync(cancellationToken))!);
        }

        await using var sharedRead = new NpgsqlCommand(
            "SELECT \"SharedSummary\" FROM engagement.\"Deals\" WHERE \"Id\" = @id", business);
        sharedRead.Parameters.AddWithValue("id", dealId);
        Assert.Equal("Copied name for business", (string)(await sharedRead.ExecuteScalarAsync(cancellationToken))!);
    }

    private async Task InsertPosterAsync(Guid businessTenantId, Guid id, bool published, CancellationToken cancellationToken)
    {
        await using var connection = await RuntimeRoleConnection.OpenAsync(
            factory.RuntimeConnectionString, businessTenantId, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            INSERT INTO public_site."Posters" ("BusinessTenantId", "Id", "IsPublished")
            VALUES (@tenant_id, @id, @published)
            """, connection, transaction);
        command.Parameters.AddWithValue("tenant_id", businessTenantId);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("published", published);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task InsertDealAsync(
        Guid consumerTenantId, Guid businessTenantId, Guid id, string summary, CancellationToken cancellationToken)
    {
        await using var connection = await RuntimeRoleConnection.OpenAsync(
            factory.RuntimeConnectionString, consumerTenantId, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            INSERT INTO engagement."Deals" ("ConsumerTenantId", "BusinessTenantId", "Id", "SharedSummary")
            VALUES (@consumer_id, @business_id, @id, @summary)
            """, connection, transaction);
        command.Parameters.AddWithValue("consumer_id", consumerTenantId);
        command.Parameters.AddWithValue("business_id", businessTenantId);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("summary", summary);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task InsertWidgetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await RuntimeRoleConnection.OpenAsync(
            factory.RuntimeConnectionString, tenantId, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            INSERT INTO tenant."Widgets" ("TenantId", "Id", "Name", "CreatedAtUtc", "IsDeleted")
            VALUES (@tenant_id, @id, 'Kevin private data', @created_at_utc, false)
            """, connection, transaction);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("created_at_utc", new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc));
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<Guid[]> VisiblePosterIdsAsync(
        NpgsqlConnection connection, Guid[] ids, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT \"Id\" FROM public_site.\"Posters\" WHERE \"Id\" = ANY(@ids)", connection);
        command.Parameters.AddWithValue("ids", ids);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var visible = new List<Guid>();
        while (await reader.ReadAsync(cancellationToken))
        {
            visible.Add(reader.GetGuid(0));
        }

        return [.. visible];
    }

    private static async Task<long> CountDealAsync(
        NpgsqlConnection connection, Guid id, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM engagement.\"Deals\" WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", id);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task<int> ExecuteForIdAsync(
        NpgsqlConnection connection, string sql, Guid id, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
