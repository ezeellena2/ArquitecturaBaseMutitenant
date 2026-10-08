using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Services.Invitations;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Invitations;

public sealed class InvitationDeliveryDestinationTests
{
    private static readonly DateTime First = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Verified_principal_phone_wins_and_otherwise_the_oldest_verified_phone_is_used()
    {
        var oldest = Row("+5491123456789", false, First);
        var principal = Row("+5491198765432", true, First.AddDays(1));
        var pending = Row("+5491112345678", true, null);
        Assert.Equal(principal.Value, InvitationDeliveryIssuer.SelectPhone([pending, oldest, principal])?.Value);
        Assert.Equal(oldest.Value, InvitationDeliveryIssuer.SelectPhone([principal with { IsPrimary = false }, oldest, pending])?.Value);
    }

    [Fact]
    public void Email_google_and_unverified_phone_are_never_used_as_a_phone_destination()
    {
        Assert.Null(InvitationDeliveryIssuer.SelectPhone([
            Row("persona@example.com", true, First) with { Type = LoginMethodType.Email },
            Row("google-subject", false, First) with { Type = LoginMethodType.Google },
            Row("+5491123456789", true, null),
        ]));
        var email = InvitationDeliveryDestination.ForEmail(Email.Create("persona@example.com").Value);
        var phone = InvitationDeliveryDestination.ForPhone(PhoneNumber.Create("+5491123456789").Value);
        Assert.Equal(LoginMethodType.Email, email.Type);
        Assert.Equal(LoginMethodType.Phone, phone.Type);
        Assert.DoesNotContain(phone.Value, phone.ToString(), StringComparison.Ordinal);
    }

    private static LoginMethodRow Row(string value, bool primary, DateTime? verified) =>
        new(Guid.CreateVersion7(), LoginMethodType.Phone, value, primary, verified, null);
}
