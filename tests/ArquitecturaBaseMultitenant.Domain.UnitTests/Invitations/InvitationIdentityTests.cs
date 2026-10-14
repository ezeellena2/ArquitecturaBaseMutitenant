using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Invitations;

/// <summary>Decisión 11 de la E11: la identidad de una invitación es un correo o un teléfono, guardada como su valor.</summary>
public sealed class InvitationIdentityTests
{
    [Fact]
    public void The_stored_value_reads_back_as_the_same_email_or_phone()
    {
        var email = InvitationIdentity.ForEmail(Email.Create("Ana@Example.test").Value);
        var phone = InvitationIdentity.ForPhone(PhoneNumber.Create("+5491134567890").Value);

        Assert.Equal(email, InvitationIdentity.Parse(email.Value));
        Assert.Equal(phone, InvitationIdentity.Parse(phone.Value));
        Assert.Equal((false, true, "+5491134567890"), (email.IsPhone, phone.IsPhone, phone.Value));
        Assert.Null(phone.Email);
        Assert.Equal(email.Email!.DisplayValue, email.DisplayValue);
    }

    [Fact]
    public void An_email_and_a_phone_are_never_the_same_identity()
    {
        var email = InvitationIdentity.ForEmail(Email.Create("ana@example.test").Value);
        var phone = InvitationIdentity.ForPhone(PhoneNumber.Create("+5491134567890").Value);

        Assert.NotEqual(email, phone);
        Assert.True(email == (InvitationIdentity)Email.Create("ana@example.test").Value);
        Assert.False(email == phone);
    }
}
