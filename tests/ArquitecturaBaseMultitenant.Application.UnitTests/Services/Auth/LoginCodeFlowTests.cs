using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Auth;

/// <summary>
/// Comprueba orden de locks, lecturas, emisión y verificación de códigos. Protege los límites de reenvío y
/// la invalidación por propósito.
/// </summary>
public sealed class LoginCodeFlowTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Issuer_locks_before_reading_and_invalidates_only_the_same_purpose()
    {
        var repository = new FakeCodes();
        var destination = Destination("ana@example.test");
        repository.Add(LoginCode.Issue(destination, LoginCodePurpose.Login, null, "old", Now.UtcDateTime.AddMinutes(-2),
            TimeSpan.FromMinutes(10), 5));
        repository.Add(LoginCode.Issue(destination, LoginCodePurpose.Signup, null, "signup", Now.UtcDateTime.AddMinutes(-2),
            TimeSpan.FromMinutes(10), 5));
        var issuer = CreateIssuer(repository);
        repository.Operations.Clear();

        var issued = await issuer.IssueLoginCodeAsync(destination, Ct);

        Assert.True(issued.IsSuccess);
        Assert.Equal("123456", issued.Value.Code);
        Assert.Equal(["lock", "times", "active", "add"], repository.Operations);
        Assert.NotNull(repository.Codes[0].InvalidatedAtUtc);
        Assert.Null(repository.Codes[1].InvalidatedAtUtc);
        Assert.Equal(LoginCodePurpose.Login, repository.Codes[2].Purpose);
        Assert.DoesNotContain("123456", issued.Value.ToString());
    }

    [Fact]
    public async Task Issuer_returns_cooldown_and_window_errors_without_generating_a_code()
    {
        var repository = new FakeCodes();
        var destination = Destination("ana@example.test");
        repository.Add(LoginCode.Issue(destination, LoginCodePurpose.Login, null, "old", Now.UtcDateTime.AddSeconds(-30),
            TimeSpan.FromMinutes(10), 5));
        var issuer = CreateIssuer(repository);

        var cooldown = await issuer.IssueLoginCodeAsync(destination, Ct);

        Assert.True(cooldown.IsFailure);
        Assert.Equal(LoginCodeErrors.ResendTooSoonCode, cooldown.Error.Code);
        Assert.Equal(30, cooldown.Error.Metadata?[LoginCodeErrors.RetryAfterKey]);
        Assert.Single(repository.Codes);

        var limited = CreateIssuer(repository, new LoginCodeOptions { MaxRequestsPerWindow = 1 });
        var window = await limited.IssueSignupCodeAsync(destination, Ct);
        Assert.True(window.IsFailure);
        Assert.Equal(LoginCodeErrors.TooManyRequestsCode, window.Error.Code);
    }

    [Fact]
    public async Task Verifier_consumes_once_and_preserves_domain_errors_for_expiry_and_attempts()
    {
        var destination = Destination("ana@example.test");
        var repository = new FakeCodes();
        var issuer = CreateIssuer(repository);
        var verifier = CreateVerifier(repository);
        var issued = await issuer.IssueLoginCodeAsync(destination, Ct);
        Assert.True(issued.IsSuccess);
        repository.Operations.Clear();

        var success = await verifier.VerifyAsync(destination, LoginCodePurpose.Login, null, "123456", Ct);
        var again = await verifier.VerifyAsync(destination, LoginCodePurpose.Login, null, "123456", Ct);

        Assert.True(success.IsSuccess);
        Assert.Equal(["lock", "latest", "lock", "latest"], repository.Operations);
        Assert.Equal(LoginCodeErrors.AlreadyUsedCode, again.Error.Code);

        var expired = LoginCode.Issue(destination, LoginCodePurpose.Signup, null, "hash", Now.UtcDateTime.AddMinutes(-20),
            TimeSpan.FromMinutes(10), 5);
        repository.Add(expired);
        var late = await verifier.VerifyAsync(destination, LoginCodePurpose.Signup, null, "123456", Ct);
        Assert.Equal(LoginCodeErrors.ExpiredCode, late.Error.Code);

        var other = Destination("otro@example.test");
        var limited = LoginCode.Issue(other, LoginCodePurpose.Login, null, "hash", Now.UtcDateTime,
            TimeSpan.FromMinutes(10), 2);
        repository.Add(limited);
        var first = await verifier.VerifyAsync(other, LoginCodePurpose.Login, null, "wrong", Ct);
        var second = await verifier.VerifyAsync(other, LoginCodePurpose.Login, null, "wrong", Ct);
        Assert.Equal(LoginCodeErrors.InvalidCode, first.Error.Code);
        Assert.Equal(LoginCodeErrors.TooManyAttemptsCode, second.Error.Code);
    }

    [Fact]
    public async Task Signup_code_cannot_verify_as_login_or_for_another_destination()
    {
        var repository = new FakeCodes();
        var destination = Destination("ana@example.test");
        var issuer = CreateIssuer(repository);
        var verifier = CreateVerifier(repository);
        Assert.True((await issuer.IssueSignupCodeAsync(destination, Ct)).IsSuccess);

        Assert.Equal(LoginCodeErrors.InvalidCode,
            (await verifier.VerifyAsync(destination, LoginCodePurpose.Login, null, "123456", Ct)).Error.Code);
        Assert.Equal(LoginCodeErrors.InvalidCode,
            (await verifier.VerifyAsync(Destination("otra@example.test"), LoginCodePurpose.Signup, null, "123456", Ct)).Error.Code);
        Assert.True((await verifier.VerifyAsync(destination, LoginCodePurpose.Signup, null, "123456", Ct)).IsSuccess);
    }

    private static LoginCodeDestination Destination(string value) => LoginCodeDestination.ForEmail(Email.Create(value).Value);

    private static LoginCodeIssuer CreateIssuer(FakeCodes repository, LoginCodeOptions? settings = null) =>
        new(repository, new Generator(), new Hasher(), Options.Create(settings ?? new LoginCodeOptions()),
            new FakeTimeProvider(Now));

    private static LoginCodeVerifier CreateVerifier(FakeCodes repository) =>
        new(repository, new Hasher(), new FakeTimeProvider(Now));

    private sealed class Generator : ILoginCodeGenerator
    {
        public string Generate() => "123456";
    }

    private sealed class Hasher : ILoginCodeHasher
    {
        public string Hash(LoginCodeDestination destination, LoginCodePurpose purpose, string code) =>
            $"{destination.Channel}|{destination.Value}|{purpose}|{code}";
    }

    private sealed class FakeCodes : ILoginCodeRepository
    {
        public List<LoginCode> Codes { get; } = [];
        public List<string> Operations { get; } = [];

        public Task LockDestinationAsync(LoginCodeDestination destination, CancellationToken cancellationToken)
        {
            Operations.Add("lock");
            return Task.CompletedTask;
        }

        public Task<LoginCode?> GetLatestAsync(LoginCodeDestination destination, LoginCodePurpose purpose,
            Guid? requestedByUserId, CancellationToken cancellationToken)
        {
            Operations.Add("latest");
            return Task.FromResult(Codes.LastOrDefault(code => Matches(code, destination, purpose, requestedByUserId)));
        }

        public Task<IReadOnlyList<LoginCode>> ListActiveAsync(LoginCodeDestination destination, LoginCodePurpose purpose,
            Guid? requestedByUserId, DateTime nowUtc, CancellationToken cancellationToken)
        {
            Operations.Add("active");
            return Task.FromResult<IReadOnlyList<LoginCode>>(Codes.Where(code =>
                Matches(code, destination, purpose, requestedByUserId) && code.IsActive(nowUtc)).ToArray());
        }

        public Task<IReadOnlyList<DateTime>> ListRequestTimesSinceAsync(LoginCodeDestination destination, DateTime sinceUtc,
            CancellationToken cancellationToken)
        {
            Operations.Add("times");
            return Task.FromResult<IReadOnlyList<DateTime>>(Codes.Where(code =>
                    code.Channel == destination.Channel && code.Destination == destination.Value && code.CreatedAtUtc >= sinceUtc)
                .Select(code => code.CreatedAtUtc).Order().ToArray());
        }

        public void Add(LoginCode code)
        {
            Operations.Add("add");
            Codes.Add(code);
        }

        private static bool Matches(LoginCode code, LoginCodeDestination destination, LoginCodePurpose purpose,
            Guid? requestedByUserId) =>
            code.Channel == destination.Channel && code.Destination == destination.Value && code.Purpose == purpose &&
            code.RequestedByUserId == requestedByUserId;
    }
}
