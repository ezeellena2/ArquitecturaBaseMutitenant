using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Authentication;

/// <summary>
/// Comprueba caducidad, intentos y uso único de los códigos. Exige que la verificación corresponda a la
/// cuenta y al propósito para los que se emitieron.
/// </summary>
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
    public void Business_signup_code_cannot_be_used_as_a_personal_signup_code()
    {
        var destination = LoginCodeDestination.ForEmail(Email.Create("empresa@example.com").Value);
        var code = LoginCode.Issue(destination, LoginCodePurpose.BusinessSignup, null, "hash", IssuedAtUtc,
            TimeSpan.FromMinutes(10), 3);

        Assert.Equal(LoginCodeErrors.InvalidCode,
            code.Verify(LoginCodePurpose.Signup, "hash", IssuedAtUtc.AddMinutes(1)).Error.Code);
        Assert.Equal(3, code.AttemptsLeft);
        Assert.True(code.Verify(LoginCodePurpose.BusinessSignup, "hash", IssuedAtUtc.AddMinutes(1)).IsSuccess);
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
    public void Recovery_code_is_bound_to_its_nonce_and_unlocks_the_request_once_verified()
    {
        var code = RecoveryCode();
        code.MarkSent(IssuedAtUtc);

        Assert.Throws<InvalidOperationException>(() => code.BindRecoveryNonce("other-nonce"));
        Assert.Equal(LoginCodeErrors.Invalid(null), code.VerifyRecovery("other-nonce", "hash", IssuedAtUtc).Error);
        Assert.Equal(LoginCodeErrors.InvalidCode, code.Verify(LoginCodePurpose.Login, "hash", IssuedAtUtc).Error.Code);
        Assert.Equal(3, code.AttemptsLeft);
        Assert.False(code.IsVerifiedRecovery("nonce", IssuedAtUtc));
        Assert.True(code.VerifyRecovery("nonce", "hash", IssuedAtUtc.AddMinutes(1)).IsSuccess);
        Assert.True(code.IsVerifiedRecovery("nonce", IssuedAtUtc.AddMinutes(2)));
        Assert.False(code.IsVerifiedRecovery("other-nonce", IssuedAtUtc.AddMinutes(2)));
        // Desde la verificación queda otro tanto como la vigencia del código para registrar el pedido.
        Assert.True(code.IsVerifiedRecovery("nonce", IssuedAtUtc.AddMinutes(10.5)));
        Assert.False(code.IsVerifiedRecovery("nonce", IssuedAtUtc.AddMinutes(11)));
        Assert.Equal(LoginCodeErrors.AlreadyUsed, code.VerifyRecovery("nonce", "hash", IssuedAtUtc.AddMinutes(2)).Error);
    }

    [Fact]
    public void Unsent_recovery_code_fails_like_a_wrong_code_and_spends_attempts()
    {
        var code = RecoveryCode();

        var first = code.VerifyRecovery("nonce", "hash", IssuedAtUtc).Error;
        Assert.Equal(LoginCodeErrors.InvalidCode, first.Code);
        Assert.Equal(2, first.Metadata![LoginCodeErrors.AttemptsLeftKey]);
        Assert.Equal(1, code.VerifyRecovery("nonce", "hash", IssuedAtUtc).Error.Metadata![LoginCodeErrors.AttemptsLeftKey]);
        Assert.Equal(LoginCodeErrors.TooManyAttempts, code.VerifyRecovery("nonce", "hash", IssuedAtUtc).Error);
        Assert.False(code.IsVerifiedRecovery("nonce", IssuedAtUtc));
    }

    [Fact]
    public void Recovery_code_has_no_account_and_only_recovery_codes_take_a_nonce()
    {
        var destination = LoginCodeDestination.ForEmail(Email.Create("ana@example.com").Value);

        Assert.Throws<ArgumentException>(() => LoginCode.Issue(destination, LoginCodePurpose.AccountRecovery,
            Guid.CreateVersion7(), "hash", IssuedAtUtc, TimeSpan.FromMinutes(10), 3));
        var login = LoginCode.Issue(destination, LoginCodePurpose.Login, null, "hash", IssuedAtUtc,
            TimeSpan.FromMinutes(10), 3);
        Assert.Throws<InvalidOperationException>(() => login.BindRecoveryNonce("nonce"));
    }

    private static LoginCode RecoveryCode()
    {
        var code = LoginCode.Issue(LoginCodeDestination.ForEmail(Email.Create("nuevo@example.com").Value),
            LoginCodePurpose.AccountRecovery, null, "hash", IssuedAtUtc, TimeSpan.FromMinutes(10), 3);
        code.BindRecoveryNonce("nonce");
        return code;
    }

    [Fact]
    public void A_phone_channel_is_known_only_when_a_module_registers_it()
    {
        var channels = new HashSet<string>(StringComparer.Ordinal) { LoginCodeChannel.Email };

        Assert.True(LoginCodeChannel.IsKnown(LoginCodeChannel.Email, channels));
        Assert.False(LoginCodeChannel.IsKnown("whatsapp", channels));
    }
}
