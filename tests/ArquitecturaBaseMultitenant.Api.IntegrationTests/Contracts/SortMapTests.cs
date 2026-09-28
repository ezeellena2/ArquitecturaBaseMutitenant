using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Contracts;

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
}
