using ArquitecturaBaseMultitenant.Application.Common.Pagination;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Common;

/// <summary>
/// Comprueba el cálculo de navegación, totales y páginas del contrato paginado. Conserva la página
/// solicitada aunque quede fuera del rango.
/// </summary>
public sealed class PaginationContractsTests
{
    private sealed record SamplePagedRequest : PagedRequest;

    private sealed record SampleCursorRequest : CursorRequest;

    [Fact]
    public void Page_request_starts_on_page_one_with_ten_rows()
    {
        var request = new SamplePagedRequest();

        Assert.Equal(1, request.Page);
        Assert.Equal(10, request.PageSize);
        Assert.Null(request.Sort);
        Assert.Null(request.Search);
        Assert.Equal(100, PagedRequest.MaxPageSize);
        Assert.Equal(100, PagedRequest.MaxSearchLength);
    }

    [Theory]
    [InlineData(1, 10, 0, 0, false, false)]
    [InlineData(1, 10, 10, 1, false, false)]
    [InlineData(1, 10, 11, 2, false, true)]
    [InlineData(2, 10, 25, 3, true, true)]
    [InlineData(3, 10, 25, 3, true, false)]
    [InlineData(8, 10, 25, 3, true, false)]
    public void Paged_result_calculates_navigation_without_rewriting_an_out_of_range_page(
        int page, int pageSize, int totalCount, int totalPages, bool hasPrevious, bool hasNext)
    {
        var result = new PagedResult<string>([], page, pageSize, totalCount);

        Assert.Equal(page, result.Page);
        Assert.Equal(totalCount, result.TotalCount);
        Assert.Equal(totalPages, result.TotalPages);
        Assert.Equal(hasPrevious, result.HasPrevious);
        Assert.Equal(hasNext, result.HasNext);
    }

    [Theory]
    [InlineData("name", "name", false)]
    [InlineData("-createdAtUtc", "createdAtUtc", true)]
    [InlineData("  -name  ", "name", true)]
    public void Sort_descriptor_parses_field_and_direction(string sort, string field, bool descending)
    {
        Assert.Equal(new SortDescriptor(field, descending), SortDescriptor.Parse(sort));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-")]
    public void Sort_descriptor_treats_missing_fields_as_no_sort(string? sort)
    {
        Assert.Null(SortDescriptor.Parse(sort));
    }

    [Fact]
    public void Cursor_contract_has_no_total_count()
    {
        var request = new SampleCursorRequest { After = "opaque", Limit = 20 };
        var result = new CursorResult<string>(["item"], "next", true);

        Assert.Equal("opaque", request.After);
        Assert.Equal(20, request.Limit);
        Assert.Equal(100, CursorRequest.MaxLimit);
        Assert.Equal(["item"], result.Items);
        Assert.Equal("next", result.NextCursor);
        Assert.True(result.HasMore);
        Assert.Null(typeof(CursorResult<string>).GetProperty("TotalCount"));
    }
}
