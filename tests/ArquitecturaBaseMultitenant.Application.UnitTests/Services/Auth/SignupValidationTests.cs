using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Validation.Auth;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Auth;

/// <summary>
/// Comprueba correo, código y aceptación de términos en los pasos del registro. Protege los nombres de
/// campo de los errores que consume el formulario.
/// </summary>
public sealed class SignupValidationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly Email ValidEmail = Email.Create(" ana@EXAMPLE.com ").Value;

    [Fact]
    public async Task Both_steps_require_the_terms_checkbox_and_bind_the_error_to_acceptedTerms()
    {
        var request = new SignupRequestValidator();
        var verify = new VerifySignupRequestValidator(Options.Create(new LoginCodeOptions()));

        var first = await request.ValidateAsync(new SignupRequest(ValidEmail, false), Ct);
        var second = await verify.ValidateAsync(new VerifySignupRequest(ValidEmail, "123456", false), Ct);

        Assert.Contains(first.Errors, error => error.PropertyName == "AcceptedTerms");
        Assert.Contains(second.Errors, error => error.PropertyName == "AcceptedTerms");
    }

    [Fact]
    public async Task Verification_requires_email_and_six_digit_code_without_a_name_field()
    {
        var validator = new VerifySignupRequestValidator(Options.Create(new LoginCodeOptions()));

        Assert.True((await validator.ValidateAsync(
            new VerifySignupRequest(ValidEmail, "123456", true), Ct)).IsValid);
        var invalid = await validator.ValidateAsync(new VerifySignupRequest(null, "12ab", true), Ct);

        Assert.Contains(invalid.Errors, error => error.PropertyName == "Email");
        Assert.Contains(invalid.Errors, error => error.PropertyName == "Code");
        Assert.DoesNotContain(typeof(SignupRequest).GetProperties(),
            property => property.Name.Contains("Name", StringComparison.Ordinal));
    }
}
