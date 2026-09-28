using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Resources;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Common;

public sealed class CursorRequestValidatorTests
{
    private sealed record AuditQuery : CursorRequest;

    private sealed class AuditQueryValidator : CursorRequestValidator<AuditQuery>;

    private static readonly AuditQueryValidator Validator = new();

    [Fact]
    public void Default_request_and_base64url_cursor_are_valid()
    {
        Assert.True(Validator.Validate(new AuditQuery()).IsValid);
        Assert.True(Validator.Validate(new AuditQuery { After = "c3RhcnQ", Limit = 100 }).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Limit_outside_one_to_one_hundred_is_rejected(int limit)
    {
        var failure = Assert.Single(Validator.Validate(new AuditQuery { Limit = limit }).Errors);

        Assert.Equal("Limit", failure.PropertyName);
        Assert.Equal(ValidationTexts.CursorLimitInvalid.Replace("{From}", "1", StringComparison.Ordinal)
            .Replace("{To}", CursorRequest.MaxLimit.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal),
            failure.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("!!!")]
    [InlineData("c3RhcnQ=")]
    public void Invalid_cursor_syntax_is_rejected_as_after_field(string after)
    {
        var failure = Assert.Single(Validator.Validate(new AuditQuery { After = after }).Errors);

        Assert.Equal("After", failure.PropertyName);
        Assert.Equal(ValidationTexts.CursorInvalid, failure.ErrorMessage);
    }
}
