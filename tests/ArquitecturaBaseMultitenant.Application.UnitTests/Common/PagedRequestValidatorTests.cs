using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Resources;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Common;

public sealed class PagedRequestValidatorTests
{
    private sealed record ProductsQuery : PagedRequest;

    private sealed class ProductsQueryValidator() : PagedRequestValidator<ProductsQuery>(["name", "createdAtUtc"]);

    private static readonly ProductsQueryValidator Validator = new();

    [Fact]
    public void Allowed_page_sizes_drive_defaults_limits_and_validation()
    {
        Assert.Equal([10, 20, 50, 100], PagedRequest.AllowedPageSizes);
        Assert.Equal(PagedRequest.AllowedPageSizes[0], PagedRequest.DefaultPageSize);
        Assert.Equal(PagedRequest.AllowedPageSizes[^1], PagedRequest.MaxPageSize);

        foreach (var size in Enumerable.Range(1, PagedRequest.MaxPageSize + 1))
        {
            Assert.Equal(PagedRequest.AllowedPageSizes.Contains(size),
                Validator.Validate(new ProductsQuery { PageSize = size }).IsValid);
        }
    }

    [Fact]
    public void Defaults_are_valid()
    {
        Assert.True(Validator.Validate(new ProductsQuery()).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1_000_001)]
    public void Page_outside_bounds_is_rejected(int page)
    {
        var failure = Assert.Single(Validator.Validate(new ProductsQuery { Page = page }).Errors);

        Assert.Equal("Page", failure.PropertyName);
        Assert.Equal(ValidationTexts.PageInvalid.Replace("{From}", "1", StringComparison.Ordinal)
            .Replace("{To}", PagedRequest.MaxPage.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal),
            failure.ErrorMessage);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public void Only_documented_page_sizes_are_accepted(int size)
    {
        Assert.True(Validator.Validate(new ProductsQuery { PageSize = size }).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(101)]
    public void Other_page_sizes_are_rejected(int size)
    {
        var failure = Assert.Single(Validator.Validate(new ProductsQuery { PageSize = size }).Errors);

        Assert.Equal("PageSize", failure.PropertyName);
        Assert.Equal(ValidationTexts.PageSizeInvalid, failure.ErrorMessage);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("-createdAtUtc")]
    [InlineData("NAME")]
    public void Whitelisted_sort_is_accepted(string sort)
    {
        Assert.True(Validator.Validate(new ProductsQuery { Sort = sort }).IsValid);
    }

    [Theory]
    [InlineData("secret")]
    [InlineData("-")]
    public void Sort_outside_whitelist_is_rejected(string sort)
    {
        var failure = Assert.Single(Validator.Validate(new ProductsQuery { Sort = sort }).Errors);

        Assert.Equal("Sort", failure.PropertyName);
        Assert.Equal(ValidationTexts.SortNotAllowed, failure.ErrorMessage);
    }

    [Fact]
    public void Search_longer_than_one_hundred_characters_is_rejected()
    {
        var failure = Assert.Single(Validator.Validate(new ProductsQuery
        {
            Search = new string('a', PagedRequest.MaxSearchLength + 1),
        }).Errors);

        Assert.Equal("Search", failure.PropertyName);
        Assert.Equal(ValidationTexts.MaxLength.Replace("{MaxLength}",
            PagedRequest.MaxSearchLength.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal),
            failure.ErrorMessage);
    }
}
