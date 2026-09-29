using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Authentication;

public sealed class LoginMethodTests
{
    [Fact]
    public void Email_method_keeps_the_canonical_value_and_starts_unverified()
    {
        var userId = Guid.CreateVersion7();
        var email = Email.Create("  ANA@Example.COM ").Value;

        var method = LoginMethod.CreateEmail(userId, email);

        Assert.Equal(userId, method.UserId);
        Assert.Equal(LoginMethodType.Email, method.Type);
        Assert.Equal("ana@example.com", method.Value);
        Assert.Null(method.VerifiedAtUtc);
        Assert.False(method.IsPrimary);
        Assert.False(method.CanSignIn(channelAvailable: true, managedMembershipActive: true));
    }

    [Fact]
    public void A_method_must_be_verified_before_it_can_be_primary_or_used_to_sign_in()
    {
        var method = LoginMethod.CreateGoogle(Guid.CreateVersion7(), "google-subject");
        var firstVerifiedAtUtc = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(LoginMethodErrors.NotVerified, method.MakePrimary().Error);
        Assert.True(method.Verify(firstVerifiedAtUtc).IsSuccess);
        Assert.True(method.Verify(firstVerifiedAtUtc.AddMinutes(1)).IsSuccess);
        Assert.Equal(firstVerifiedAtUtc, method.VerifiedAtUtc);
        Assert.True(method.MakePrimary().IsSuccess);
        Assert.True(method.IsPrimary);
        Assert.True(method.CanSignIn(channelAvailable: true, managedMembershipActive: true));
        Assert.False(method.CanSignIn(channelAvailable: false, managedMembershipActive: true));
    }

    [Fact]
    public void Phone_is_a_method_type_and_does_not_enable_a_sign_in_channel()
    {
        var phone = PhoneNumber.Create("+5491123456789").Value;
        var method = LoginMethod.CreatePhone(Guid.CreateVersion7(), phone);

        Assert.Equal(LoginMethodType.Phone, method.Type);
        Assert.Equal(phone.Value, method.Value);
        Assert.True(method.Verify(new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc)).IsSuccess);
        Assert.False(method.CanSignIn(channelAvailable: false, managedMembershipActive: true));
    }

    [Fact]
    public void Unmanaged_method_is_independent_of_business_membership()
    {
        var method = LoginMethod.CreateEmail(Guid.CreateVersion7(), Email.Create("ana@example.com").Value);
        method.Verify(new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc));

        Assert.True(method.CanSignIn(channelAvailable: true, managedMembershipActive: false));
    }
}
