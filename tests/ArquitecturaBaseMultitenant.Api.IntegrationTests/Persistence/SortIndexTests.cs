using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

[Collection(ApiTestGroup.Name)]
public sealed class SortIndexTests(ApiFactory factory)
{
    [Fact]
    public async Task Every_widget_sort_field_has_tenant_first_index_with_id_tie_breaker()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await WidgetQueryFixture.CreateAsync(factory, Guid.NewGuid(), cancellationToken);
        var entity = fixture.Context.Model.FindEntityType(typeof(Widget));
        Assert.NotNull(entity);

        foreach (var sortField in WidgetQueryFixture.SortableFields)
        {
            var member = sortField.Value.Body is System.Linq.Expressions.UnaryExpression unary
                ? unary.Operand : sortField.Value.Body;
            var propertyName = Assert.IsAssignableFrom<System.Linq.Expressions.MemberExpression>(member).Member.Name;
            Assert.Contains(entity.GetIndexes(), index =>
                index.Properties.Select(property => property.Name)
                    .SequenceEqual(["TenantId", propertyName, "Id"]));
        }

        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT indexdef FROM pg_indexes WHERE schemaname = 'tenant' AND tablename = 'Widgets'
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var definitions = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            definitions.Add(reader.GetString(0));
        }

        Assert.Contains(definitions, definition => definition.Contains("\"TenantId\", \"Name\", \"Id\"", StringComparison.Ordinal));
        Assert.Contains(definitions, definition => definition.Contains("\"TenantId\", \"CreatedAtUtc\", \"Id\"", StringComparison.Ordinal));
        Assert.Contains(definitions, definition => definition.Contains("USING gin", StringComparison.OrdinalIgnoreCase)
            && definition.Contains("f_unaccent", StringComparison.Ordinal)
            && definition.Contains("\"Name\"", StringComparison.Ordinal)
            && definition.Contains("gin_trgm_ops", StringComparison.Ordinal));
    }
}
