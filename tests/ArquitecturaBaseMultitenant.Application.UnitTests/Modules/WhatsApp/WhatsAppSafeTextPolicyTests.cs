using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.WhatsApp;

public sealed class WhatsAppSafeTextPolicyTests
{
    [Theory]
    [InlineData("1234")]
    [InlineData("12345")]
    [InlineData("123456")]
    [InlineData("1234567")]
    [InlineData("12345678")]
    [InlineData("123456789")]
    [InlineData("1234567890")]
    [InlineData("12-34")]
    [InlineData("12.345")]
    [InlineData("12-34-56")]
    [InlineData("1 2 3 4")]
    [InlineData("1 2 3 4 5")]
    [InlineData("1 2 3 4 5 6")]
    [InlineData("Mi código es 1234, gracias")]
    [InlineData("Código: 12345")]
    [InlineData("Código: 123456")]
    public void All_admitted_OTP_lengths_and_common_separators_are_never_retained(string text) =>
        Assert.Null(WhatsAppSafeTextPolicy.Retain(text));

    [Theory]
    [InlineData("Hola, qué tal")]
    [InlineData("La mesa es 123")]
    public void Human_text_without_a_credential_remains_available(string text) =>
        Assert.Equal(text, WhatsAppSafeTextPolicy.Retain(text));
}
