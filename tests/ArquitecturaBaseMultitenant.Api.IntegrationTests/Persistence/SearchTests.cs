using System.Data.Common;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

/// <summary>
/// Comprueba búsqueda sin distinción de mayúsculas o acentos y con comodines tratados como texto. Mantiene
/// el filtro del espacio al combinar columnas.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class SearchTests(ApiFactory factory)
{
    [Theory]
    [InlineData("perez", "Pérez")]
    [InlineData("PEREZ", "Pérez")]
    [InlineData("%", "100% real")]
    [InlineData("_", "A_B")]
    [InlineData("\\", "Ruta\\Sur")]
    public async Task Search_is_accent_and_case_insensitive_but_treats_wildcards_as_literals(
        string search, string expectedName)
    {
        var tenantId = Guid.NewGuid();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await WidgetQueryFixture.CreateAsync(factory, tenantId, cancellationToken);
        var names = new[] { "Pérez", "100% real", "100 cosas", "A_B", "AXB", "Ruta\\Sur" };
        foreach (var name in names)
        {
            await fixture.AddAsync(tenantId, Guid.CreateVersion7(), name,
                new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc), cancellationToken);
        }

        var matches = await fixture.Rows.ApplySearch(search, widget => widget.Name)
            .Select(widget => widget.Name)
            .ToArrayAsync(cancellationToken);

        Assert.Equal([expectedName], matches);
    }

    [Fact]
    public async Task Search_combines_columns_with_or_and_keeps_tenant_filter()
    {
        var tenantId = Guid.NewGuid();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await WidgetQueryFixture.CreateAsync(factory, tenantId, cancellationToken);
        await fixture.AddAsync(tenantId, Guid.CreateVersion7(), "Nombre propio",
            new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc), cancellationToken);

        var query = fixture.Rows.ApplySearch("propio", widget => widget.Name.Substring(0, 1), widget => widget.Name);
        var sql = query.ToQueryString();
        Assert.Contains("f_unaccent", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIKE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(" OR ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TenantId", sql, StringComparison.Ordinal);
        Assert.Single(await query.ToArrayAsync(cancellationToken));
    }

    [Fact]
    public async Task Search_generates_identical_parameterized_sql_for_different_terms()
    {
        var tenantId = Guid.NewGuid();
        var cancellationToken = TestContext.Current.CancellationToken;
        var capture = new SqlCaptureInterceptor();
        await using var fixture = await WidgetQueryFixture.CreateAsync(factory, tenantId, cancellationToken, capture);
        var stampUtc = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);
        await fixture.AddAsync(tenantId, Guid.CreateVersion7(), "Álvarez", stampUtc, cancellationToken);
        await fixture.AddAsync(tenantId, Guid.CreateVersion7(), "Zapata", stampUtc, cancellationToken);

        capture.Clear();
        var first = await fixture.Rows.ApplySearch("alvarez", widget => widget.Name)
            .Select(widget => widget.Name).ToArrayAsync(cancellationToken);
        var firstSql = Assert.Single(capture.Commands);

        capture.Clear();
        var second = await fixture.Rows.ApplySearch("zapata", widget => widget.Name)
            .Select(widget => widget.Name).ToArrayAsync(cancellationToken);
        var secondSql = Assert.Single(capture.Commands);

        Assert.Equal(["Álvarez"], first);
        Assert.Equal(["Zapata"], second);
        Assert.Equal(firstSql, secondSql);
        Assert.DoesNotContain("alvarez", firstSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("zapata", secondSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("f_unaccent", firstSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIKE", firstSql, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class SqlCaptureInterceptor : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public void Clear() => Commands.Clear();

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Commands.Add(command.CommandText);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
