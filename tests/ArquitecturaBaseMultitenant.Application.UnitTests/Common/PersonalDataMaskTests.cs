using ArquitecturaBaseMultitenant.Application.Common.Text;
using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Common;

/// <summary>
/// Un solo formato para correos y teléfonos enmascarados, el de los tableros: «lu•••@delta.ejemplo.com» y
/// «•••• 4521».
/// </summary>
public sealed class PersonalDataMaskTests
{
    [Theory]
    [InlineData("lucia.fernandez@delta.ejemplo.com", "lu•••@delta.ejemplo.com")]
    [InlineData("pablo@kappa.ejemplo.com", "pa•••@kappa.ejemplo.com")]
    [InlineData("ab@x.test", "a•••@x.test")]
    [InlineData("a@x.test", "a•••@x.test")]
    public void Email_keeps_the_domain_and_hides_most_of_the_local_part(string email, string masked)
    {
        Assert.Equal(masked, PersonalDataMask.Email(email));
    }

    [Theory]
    [InlineData("")]
    [InlineData("sin-arroba")]
    [InlineData("@solo-dominio.test")]
    [InlineData("sin-dominio@")]
    public void A_value_that_is_not_an_email_is_fully_hidden(string value)
    {
        Assert.Equal("•••", PersonalDataMask.Email(value));
    }

    [Theory]
    [InlineData("lu•••@delta.ejemplo.com", "lu•••@delta.ejemplo.com")]
    [InlineData("***@example.test", "•••@example.test")]
    [InlineData("l***@gmail.com", "l•••@gmail.com")]
    public void An_email_that_is_already_masked_never_shows_more(string value, string masked)
    {
        // La auditoría vuelve a enmascarar al leer lo que el interceptor ya enmascaró, y hay entradas con el formato viejo.
        Assert.Equal(masked, PersonalDataMask.Email(value));
    }

    [Theory]
    [InlineData("+5491143214521", "•••• 4521")]
    [InlineData("+541145678900", "•••• 8900")]
    [InlineData("+12015550123", "•••• 0123")]
    [InlineData("+54 9 11 4321-4521", "•••• 4521")]
    public void Phone_shows_only_the_last_four_digits(string phone, string masked)
    {
        Assert.Equal(masked, PersonalDataMask.Phone(phone));
    }

    [Theory]
    [InlineData("•••• 4521", "•••• 4521")]
    [InlineData("***8900", "•••• 8900")]
    [InlineData("+54 9 11 •••• 6789", "•••• 6789")]
    public void A_phone_that_is_already_masked_keeps_the_same_format(string value, string masked)
    {
        Assert.Equal(masked, PersonalDataMask.Phone(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("+54")]
    [InlineData("sin números")]
    public void A_phone_with_fewer_than_four_digits_is_fully_hidden(string value)
    {
        Assert.Equal("••••", PersonalDataMask.Phone(value));
    }

    [Fact]
    public void Null_is_a_bug_and_not_a_value_to_mask()
    {
        Assert.Throws<ArgumentNullException>(() => PersonalDataMask.Email(null!));
        Assert.Throws<ArgumentNullException>(() => PersonalDataMask.Phone(null!));
    }

    [Theory]
    [InlineData(LoginMethodType.Email, "lucia.fernandez@delta.ejemplo.com", null, "lu•••@delta.ejemplo.com")]
    [InlineData(LoginMethodType.Phone, "+5491143214521", null, "•••• 4521")]
    [InlineData(LoginMethodType.Google, "google-subject-123", "lucia@gmail.com", "lu•••@gmail.com")]
    [InlineData(LoginMethodType.Google, "google-subject-123", null, "Google")]
    public void A_login_method_is_masked_by_its_type(LoginMethodType type, string value, string? contactEmail,
        string masked)
    {
        // Google se muestra por su correo de contacto; el subject nunca sale.
        Assert.Equal(masked, PersonalDataMask.LoginMethod(type, value, contactEmail));
    }
}
