using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Validation.Auth;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Auth;

/// <summary>
/// Comprueba los datos de solicitud y verificación del código de ingreso. Exige correo válido, seis dígitos
/// y un retorno local autorizado.
/// </summary>
public sealed class LoginCodeValidationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly Email ValidEmail = Email.Create(" ANA@Example.com ").Value;

    [Fact]
    public async Task Request_needs_a_validated_email_and_response_does_not_identify_the_account()
    {
        var validator = new RequestLoginCodeRequestValidator();

        Assert.False((await validator.ValidateAsync(new RequestLoginCodeRequest(null), Ct)).IsValid);
        Assert.True((await validator.ValidateAsync(new RequestLoginCodeRequest(ValidEmail), Ct)).IsValid);
        Assert.Equal("ana@example.com", ValidEmail.Value);
        Assert.DoesNotContain(typeof(RequestLoginCodeResponse).GetProperties(),
            property => property.Name.Contains("Email", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("123456", "/connect/authorize?client_id=web", true)]
    [InlineData("12345", "/connect/authorize?client_id=web", false)]
    [InlineData("12345a", "/connect/authorize?client_id=web", false)]
    [InlineData("123456", "https://evil.example/connect/authorize", false)]
    [InlineData("123456", "//evil.example", false)]
    public async Task Verification_requires_six_digits_and_local_authorize_return(
        string code, string returnUrl, bool expectedValid)
    {
        var validator = new VerifyLoginCodeRequestValidator(Options.Create(new LoginCodeOptions()));

        var result = await validator.ValidateAsync(new VerifyLoginCodeRequest(ValidEmail, code, returnUrl), Ct);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public async Task Verification_needs_email_and_code()
    {
        var validator = new VerifyLoginCodeRequestValidator(Options.Create(new LoginCodeOptions()));

        var result = await validator.ValidateAsync(
            new VerifyLoginCodeRequest(null, null, "/connect/authorize"), Ct);

        Assert.Contains(result.Errors, error => error.PropertyName == "Email");
        Assert.Contains(result.Errors, error => error.PropertyName == "Code");
    }
}
