using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Contracts;

/// <summary>
/// Comprueba que el orden solicitado use campos permitidos y direcciones válidas, con la regla de todas las tablas
/// (decisión 264): lo vacío al final en los dos sentidos, un estado por su circuito y, con el mismo valor, el orden de
/// siempre de la lista y el desempate por identificador al paginar.
/// </summary>
public sealed class SortMapTests
{
    private sealed record Row(int Id, string Name, int Rank);

    private static readonly SortMap<Row> SortableFields = new()
    {
        ["name"] = row => row.Name,
        ["rank"] = row => row.Rank,
    };

    private static readonly SortDescriptor DefaultSort = new("rank", Descending: false);

    private static readonly IQueryable<Row> Rows = new[]
    {
        new Row(3, "Ana", 1),
        new Row(1, "Cata", 1),
        new Row(2, "Beto", 2),
    }.AsQueryable();

    [Theory]
    [InlineData("name", new[] { 3, 2, 1 })]
    [InlineData("-name", new[] { 1, 2, 3 })]
    [InlineData("NAME", new[] { 3, 2, 1 })]
    public void Sort_uses_only_mapped_fields_and_direction(string sort, int[] expectedIds)
    {
        var ids = Rows.ApplySort(SortDescriptor.Parse(sort), SortableFields, DefaultSort, row => row.Id)
            .Select(row => row.Id)
            .ToArray();

        Assert.Equal(expectedIds, ids);
    }

    [Fact]
    public void Default_sort_and_unique_id_keep_equal_keys_in_stable_order()
    {
        var ids = Rows.ApplySort(null, SortableFields, DefaultSort, row => row.Id)
            .Select(row => row.Id)
            .ToArray();

        Assert.Equal([1, 3, 2], ids);
    }

    [Fact]
    public void Sort_rejects_a_field_outside_the_map()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Rows.ApplySort(new SortDescriptor("secret", false), SortableFields, DefaultSort, row => row.Id));
    }

    private enum Stage
    {
        Received,
        Approved,
        Cancelled,
    }

    private sealed record Item(int Id, string? Name, int Rank, Stage? Stage, string? Prefix, int? Number);

    private static readonly SortMap<Item> ItemFields = new()
    {
        ["name"] = item => item.Name,
        ["rank"] = item => item.Rank,
        ["stage"] = item => item.Stage,
        ["number"] = item => new { item.Prefix, item.Number },
    };

    private static readonly IQueryable<Item> Items = new[]
    {
        new Item(1, "Beto", 2, Stage.Cancelled, "B", 1),
        new Item(2, null, 1, null, "C", null),
        new Item(3, "Ana", 3, Stage.Approved, "A", 2),
        new Item(4, null, 3, Stage.Received, "A", null),
        new Item(5, "Cata", 1, Stage.Received, "A", 1),
    }.AsQueryable();

    private static int[] SortItems(string sort) =>
        [.. Items.ApplySort(SortDescriptor.Parse(sort), ItemFields, new SortDescriptor("rank", Descending: true), item => item.Id)
            .Select(item => item.Id)];

    [Theory]
    [InlineData("name", new[] { 3, 1, 5, 4, 2 })]
    [InlineData("-name", new[] { 5, 1, 3, 4, 2 })]
    public void Empty_values_go_last_in_both_directions_and_keep_the_usual_order(string sort, int[] expected) =>
        Assert.Equal(expected, SortItems(sort));

    [Theory]
    [InlineData("stage", new[] { 4, 5, 3, 1, 2 })]
    [InlineData("-stage", new[] { 1, 3, 4, 5, 2 })]
    public void A_state_sorts_by_its_circuit_and_not_by_its_name(string sort, int[] expected) =>
        Assert.Equal(expected, SortItems(sort));

    [Theory]
    [InlineData("number", new[] { 5, 3, 1, 4, 2 })]
    [InlineData("-number", new[] { 1, 3, 5, 2, 4 })]
    public void A_field_with_several_keys_sorts_by_each_and_is_empty_when_one_is_missing(string sort, int[] expected) =>
        Assert.Equal(expected, SortItems(sort));

    [Theory]
    [InlineData("rank", new[] { 2, 5, 1, 3, 4 })]
    [InlineData("-rank", new[] { 3, 4, 1, 2, 5 })]
    public void Asking_for_the_usual_field_uses_the_asked_direction_and_the_id(string sort, int[] expected) =>
        Assert.Equal(expected, SortItems(sort));

    [Fact]
    public void Sql_puts_empty_values_last_and_maps_a_state_saved_as_text_to_its_circuit()
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=not-opened;Username=test;Password=test").Options, new NoTenant());
        var fields = new SortMap<Company>
        {
            ["name"] = company => company.Name,
            ["taxId"] = company => company.TaxNumber,
            ["status"] = company => company.Status,
        };
        string OrderBy(string field, bool descending)
        {
            var sql = context.Companies.ApplySort(new SortDescriptor(field, descending), fields,
                new SortDescriptor("name", false), company => company.Id).ToQueryString();
            return sql[sql.IndexOf("ORDER BY", StringComparison.Ordinal)..];
        }

        var byStatus = OrderBy("status", false);
        var byTaxIdDescending = OrderBy("taxId", true);
        var byTaxIdAscending = OrderBy("taxId", false);

        Assert.Contains("\"Status\" = 'Active' THEN 0", byStatus, StringComparison.Ordinal);
        Assert.Contains("\"Status\" = 'Inactive' THEN 1", byStatus, StringComparison.Ordinal);
        Assert.Contains("\"TaxNumber\" IS NULL", byTaxIdDescending, StringComparison.Ordinal);
        Assert.True(byTaxIdDescending.IndexOf("IS NULL", StringComparison.Ordinal)
            < byTaxIdDescending.IndexOf("DESC", StringComparison.Ordinal));
        // Un ASC de PostgreSQL ya deja NULL al final: la clave no se repite, que importa cuando es una subconsulta.
        Assert.DoesNotContain("IS NULL", byTaxIdAscending, StringComparison.Ordinal);
        Assert.EndsWith("\"Name\", c.\"Id\"", byTaxIdAscending.TrimEnd(), StringComparison.Ordinal);
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No active tenant.");
    }
}
