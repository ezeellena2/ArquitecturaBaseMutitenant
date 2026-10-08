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

    private sealed class SignIn : ISignInService
    {
        internal int Failures { get; private set; }
        public Task<bool> IsLockedOutAsync(Guid userId, CancellationToken ct) => Task.FromResult(Failures > 0);
        public Task RegisterFailedAttemptAsync(Guid userId, CancellationToken ct) { Failures++; return Task.CompletedTask; }
        public Task ResetFailedAttemptsAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeSessionsAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();
        public Task SignInAsync(Guid userId, Access access, CancellationToken ct) => throw new NotSupportedException();
        public Task<ExternalLogin?> GetExternalLoginAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task SignOutExternalAsync(CancellationToken ct) => throw new NotSupportedException();
    }
}
