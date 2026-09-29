using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Authentication;

public sealed class LoginCodeTests
{
    private static readonly DateTime IssuedAtUtc = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Signup_code_can_be_issued_and_used_once()
    {
        var destination = LoginCodeDestination.ForEmail(Email.Create("ANA@Example.com").Value);
        var code = LoginCode.Issue(destination, LoginCodePurpose.Signup, null, "hash", IssuedAtUtc,
            TimeSpan.FromMinutes(10), 3);

        Assert.Equal("email", code.Channel.ToString());
        Assert.Equal("ana@example.com", code.Destination);
        Assert.Equal(LoginCodeErrors.InvalidCode,
            code.Verify(LoginCodePurpose.Login, "hash", IssuedAtUtc.AddMinutes(1)).Error.Code);
        Assert.True(code.Verify(LoginCodePurpose.Signup, "hash", IssuedAtUtc.AddMinutes(1)).IsSuccess);
        Assert.Equal(LoginCodeErrors.AlreadyUsed,
            code.Verify(LoginCodePurpose.Signup, "hash", IssuedAtUtc.AddMinutes(2)).Error);
    }

    [Fact]
    public void Code_expires_at_the_boundary_and_consumes_failed_attempts()
    {
        var destination = LoginCodeDestination.ForEmail(Email.Create("ana@example.com").Value);
        var code = LoginCode.Issue(destination, LoginCodePurpose.Login, null, "correct-hash", IssuedAtUtc,
            TimeSpan.FromMinutes(10), 2);

        var firstFailure = code.Verify(LoginCodePurpose.Login, "wrong-hash", IssuedAtUtc.AddMinutes(1));
        Assert.Equal(LoginCodeErrors.InvalidCode, firstFailure.Error.Code);
        Assert.Equal(1, firstFailure.Error.Metadata?[LoginCodeErrors.AttemptsLeftKey]);
        Assert.Equal(1, code.AttemptsLeft);
        Assert.Equal(LoginCodeErrors.TooManyAttempts,
            code.Verify(LoginCodePurpose.Login, "wrong-again", IssuedAtUtc.AddMinutes(2)).Error);
        Assert.Equal(0, code.AttemptsLeft);
        Assert.Equal(LoginCodeErrors.TooManyAttempts,
            code.Verify(LoginCodePurpose.Login, "correct-hash", IssuedAtUtc.AddMinutes(3)).Error);

        var expired = LoginCode.Issue(destination, LoginCodePurpose.Login, null, "hash", IssuedAtUtc,
            TimeSpan.FromMinutes(10), 3);
        Assert.Equal(LoginCodeErrors.Expired,
            expired.Verify(LoginCodePurpose.Login, "hash", IssuedAtUtc.AddMinutes(10)).Error);
    }

    [Fact]
    public void Verification_code_cannot_be_used_by_another_account()
    {
        var userId = Guid.CreateVersion7();
        var code = LoginCode.Issue(LoginCodeDestination.ForEmail(Email.Create("ana@example.com").Value),
            LoginCodePurpose.VerifyDestination, userId, "hash", IssuedAtUtc, TimeSpan.FromMinutes(10), 3);

        Assert.Equal(LoginCodeErrors.Invalid(null),
            code.VerifyFor(Guid.CreateVersion7(), LoginCodePurpose.VerifyDestination, "hash", IssuedAtUtc).Error);
        Assert.Equal(3, code.AttemptsLeft);
        Assert.True(code.VerifyFor(userId, LoginCodePurpose.VerifyDestination, "hash", IssuedAtUtc).IsSuccess);
    }

    [Fact]
    public void Reauthentication_code_needs_its_account_and_exact_purpose()
    {
        var userId = Guid.CreateVersion7();
        var code = LoginCode.Issue(LoginCodeDestination.ForEmail(Email.Create("ana@example.com").Value),
            LoginCodePurpose.Reauthenticate, userId, "hash", IssuedAtUtc, TimeSpan.FromMinutes(5), 3);

        Assert.Equal(LoginCodeErrors.InvalidCode,
            code.VerifyFor(userId, LoginCodePurpose.VerifyDestination, "hash", IssuedAtUtc).Error.Code);
        Assert.Equal(3, code.AttemptsLeft);
        Assert.True(code.VerifyFor(userId, LoginCodePurpose.Reauthenticate, "hash", IssuedAtUtc).IsSuccess);
    }

    [Fact]
    public void A_phone_channel_is_known_only_when_a_module_registers_it()
    {
        var channels = new HashSet<string>(StringComparer.Ordinal) { LoginCodeChannel.Email };

        Assert.True(LoginCodeChannel.IsKnown(LoginCodeChannel.Email, channels));
        Assert.False(LoginCodeChannel.IsKnown("whatsapp", channels));
    }
}
