using ArquitecturaBaseMultitenant.Application.Common.Text;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Common;

/// <summary>Correos enmascarados como en los tableros de Plataforma: «lu•••@delta.ejemplo.com».</summary>
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
    public void A_value_that_is_not_an_email_is_fully_hidden(string value)
    {
        Assert.Equal("•••", PersonalDataMask.Email(value));
    }
}
