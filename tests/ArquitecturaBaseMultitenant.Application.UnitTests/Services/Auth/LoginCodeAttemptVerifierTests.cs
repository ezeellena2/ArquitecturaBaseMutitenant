using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Auth;

public sealed class LoginCodeAttemptVerifierTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(LoginCodePurpose.Login)]
    [InlineData(LoginCodePurpose.Signup)]
    [InlineData(LoginCodePurpose.BusinessSignup)]
    public async Task Only_an_active_failed_code_counts_towards_account_lockout(LoginCodePurpose purpose)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
        var codes = new LoginCodeFlowTests.FakeCodes();
        var hasher = new LoginCodeFlowTests.Hasher();
        var signIn = new SignIn();
        var destination = LoginCodeDestination.ForEmail(Email.Create("owner@example.test").Value);
        var accountId = Guid.CreateVersion7();
        var verifier = new LoginCodeAttemptVerifier(new LoginCodeVerifier(codes, hasher, clock), codes, signIn, clock);
        await verifier.VerifyAsync(destination, purpose, null, "654321", accountId, Ct);
        Assert.Equal(0, signIn.Failures);
        codes.Add(LoginCode.Issue(destination, purpose, null, hasher.Hash(destination, purpose, "123456"),
            clock.GetUtcNow().UtcDateTime, TimeSpan.FromMinutes(10), 5));
        var failed = await verifier.VerifyAsync(destination, purpose, null, "654321", accountId, Ct);
        Assert.Equal(AccountErrors.LockedOut.Code, failed.Error.Code);
        Assert.Equal(1, signIn.Failures);
        clock.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(LoginCodeErrors.Expired.Code,
            (await verifier.VerifyAsync(destination, purpose, null, "654321", accountId, Ct)).Error.Code);
        Assert.Equal(1, signIn.Failures);
    }

    // B4: con la cuenta bloqueada, el código correcto dice LockedOut y no se gasta; uno equivocado sigue gastando
    // un intento, así el bloqueo no deja probar gratis.
    [Theory]
    [InlineData(LoginCodePurpose.Login, false)]
    [InlineData(LoginCodePurpose.Login, true)]
    [InlineData(LoginCodePurpose.Signup, false)]
    [InlineData(LoginCodePurpose.BusinessSignup, false)]
    [InlineData(LoginCodePurpose.BusinessSignup, true)]
    public async Task A_correct_code_on_a_locked_account_says_LockedOut_and_is_not_consumed(
        LoginCodePurpose purpose, bool requestedByAccount)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
        var codes = new LoginCodeFlowTests.FakeCodes();
        var hasher = new LoginCodeFlowTests.Hasher();
        var signIn = new SignIn { Locked = true };
        var destination = LoginCodeDestination.ForEmail(Email.Create("owner@example.test").Value);
        var accountId = Guid.CreateVersion7();
        Guid? requestedBy = requestedByAccount ? accountId : null;
        var issued = LoginCode.Issue(destination, purpose, requestedBy, hasher.Hash(destination, purpose, "123456"),
            clock.GetUtcNow().UtcDateTime, TimeSpan.FromMinutes(10), 5);
        issued.MarkSent(clock.GetUtcNow().UtcDateTime);
        codes.Add(issued);
        var verifier = new LoginCodeAttemptVerifier(new LoginCodeVerifier(codes, hasher, clock), codes, signIn, clock);

        var correct = await verifier.VerifyAsync(destination, purpose, requestedBy, "123456", accountId, Ct);
        Assert.Equal(AccountErrors.LockedOutCode, correct.Error.Code);
        Assert.Null(issued.ConsumedAtUtc);
        Assert.Equal(0, issued.FailedAttempts);
        Assert.Equal(0, signIn.Failures);

        var wrong = await verifier.VerifyAsync(destination, purpose, requestedBy, "654321", accountId, Ct);
        Assert.Equal(AccountErrors.LockedOutCode, wrong.Error.Code);
        Assert.Equal(1, issued.FailedAttempts);
        Assert.Equal(1, signIn.Failures);

        signIn.Locked = false;
        Assert.True((await verifier.VerifyAsync(destination, purpose, requestedBy, "123456", accountId, Ct)).IsSuccess);
        Assert.NotNull(issued.ConsumedAtUtc);
    }

    private sealed class SignIn : ISignInService
    {
        internal int Failures { get; private set; }
        internal bool? Locked { get; set; }
        public Task<bool> IsLockedOutAsync(Guid userId, CancellationToken ct) => Task.FromResult(Locked ?? Failures > 0);
        public Task RegisterFailedAttemptAsync(Guid userId, CancellationToken ct) { Failures++; return Task.CompletedTask; }
        public Task ResetFailedAttemptsAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeSessionsAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();
        public Task SignInAsync(Guid userId, Access access, CancellationToken ct) => throw new NotSupportedException();
        public Task SignInPlatformAsync(Guid userId, string sessionId, DateTime startedAtUtc, CancellationToken ct) => throw new NotSupportedException();
        public Task<string> GetSecurityStampAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<DateTime?> GetLockoutEndUtcAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ExternalLogin?> GetExternalLoginAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task SignOutExternalAsync(CancellationToken ct) => throw new NotSupportedException();
    }
}
