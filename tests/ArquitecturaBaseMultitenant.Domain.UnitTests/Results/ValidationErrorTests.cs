using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Results;

public sealed class ValidationErrorTests
{
    [Fact]
    public void Error_factories_set_the_type_and_keep_metadata()
    {
        Assert.Equal(ErrorType.Failure, Error.Failure("General.Test.Failed", "Failed.").Type);
        Assert.Equal(ErrorType.Validation, Error.Validation("General.Test.Invalid", "Invalid.").Type);
        Assert.Equal(ErrorType.Unauthorized, Error.Unauthorized("General.Test.Unauthorized", "Unauthorized.").Type);
        Assert.Equal(ErrorType.Forbidden, Error.Forbidden("General.Test.Forbidden", "Forbidden.").Type);
        Assert.Equal(ErrorType.NotFound, Error.NotFound("General.Test.NotFound", "Not found.").Type);
        Assert.Equal(ErrorType.Conflict, Error.Conflict("General.Test.Conflict", "Conflict.").Type);

        var rateLimited = Error.TooManyRequests(
            "General.Test.RateLimited", "Too many requests.",
            new Dictionary<string, object?> { ["retryAfter"] = 30 });

        Assert.Equal(ErrorType.TooManyRequests, rateLimited.Type);
        Assert.Equal(30, rateLimited.Metadata!["retryAfter"]);
    }

    [Fact]
    public void Validation_error_groups_messages_by_field()
    {
        var errors = new Dictionary<string, string[]> { ["email"] = ["Invalid.", "Required."] };

        var result = new ValidationError(errors);

        Assert.Equal(ValidationError.ErrorCode, result.Code);
        Assert.Equal(ErrorType.Validation, result.Type);
        Assert.Equal(errors["email"], result.Errors["email"]);
    }

    [Fact]
    public void Business_rule_validation_keeps_its_code_and_field_errors()
    {
        var result = new ValidationError(
            "Users.Invitation.ConsentRequired",
            "The person must have accepted WhatsApp messages.",
            new Dictionary<string, string[]> { ["invitation.consent"] = ["Confirm it."] });

        Assert.Equal("Users.Invitation.ConsentRequired", result.Code);
        Assert.Equal("The person must have accepted WhatsApp messages.", result.Description);
        Assert.Equal(ErrorType.Validation, result.Type);
        Assert.Equal("Confirm it.", Assert.Single(result.Errors["invitation.consent"]));
    }
}
