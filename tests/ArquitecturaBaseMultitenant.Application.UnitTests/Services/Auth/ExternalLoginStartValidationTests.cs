using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Validation.Auth;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Auth;

/// <summary>El desafío conserva consentimiento, verbo y retornos seguros antes del state OAuth.</summary>
public sealed class ExternalLoginStartValidationTests
{
    [Theory]
    [InlineData(true, false, true, "/", "Signup")]
    [InlineData(false, true, true, "/", "Signup")]
    [InlineData(true, true, false, "/", "AcceptedTerms")]
    [InlineData(true, true, true, "//external.test", "ReturnTo")]
    [InlineData(true, true, true, "/\\external.test", "ReturnTo")]
    [InlineData(true, true, true, "/\n", "ReturnTo")]
    public async Task Invalid_signup_does_not_start_the_challenge(bool signup, bool signupEndpoint,
        bool terms, string returnTo, string field)
    {
        var validator = new ExternalLoginStartRequestValidator();
        var result = await validator.ValidateAsync(new ExternalLoginStartRequest(null, signup, terms,
            returnTo, signupEndpoint), TestContext.Current.CancellationToken);
        Assert.Contains(result.Errors, error => error.PropertyName == field);
    }

    [Theory]
    [InlineData("/invitacion", true)]
    [InlineData("/connect/authorize?access=business", true)]
    [InlineData("https://external.test", false)]
    [InlineData("/connect/authorize?access=unexpected", false)]
    public async Task Login_only_accepts_validated_returns(string returnUrl, bool valid)
    {
        var validator = new ExternalLoginStartRequestValidator();
        var result = await validator.ValidateAsync(new ExternalLoginStartRequest(returnUrl, false, false,
            null, false), TestContext.Current.CancellationToken);
        Assert.Equal(valid, result.IsValid);
    }
}
