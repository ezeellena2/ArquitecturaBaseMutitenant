using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Application.Validation.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Auth;
public sealed class LoginLinkServiceTests
{
    [Theory]
    [InlineData("operator")]
    [InlineData("removed")]
    [InlineData("consumed")]
    public async Task Preview_rereads_current_identity_after_awaited_lockout_without_UoW(string mutation)
    {
        using var fixture = new ServiceFixture<LoginLinkService>(new PreviewLoginLinkRequestValidator());
        var user = Guid.NewGuid(); var method = Guid.NewGuid(); var nowUtc = fixture.TimeProvider.GetUtcNow().UtcDateTime;
        var repo = new Links { Snapshot = new(user, method, nowUtc.AddMinutes(10), null, null,
            new UserAccountRow(user, "Ana", "es-AR", "UTC", UserStatus.Active, false, null, null),
            new LoginMethodRow(method, LoginMethodType.Phone, "+5491134567890", true, nowUtc, null, null, null), true, null) };
        var signIn = new SignIn(() => repo.Snapshot = mutation switch
        {
            "operator" => repo.Snapshot! with { Account = repo.Snapshot!.Account with { IsPlatformOperator = true } },
            "consumed" => repo.Snapshot! with { ConsumedAtUtc = nowUtc },
            _ => null,
        });
        var guard = new LoginLinkGuard(repo, null!, new LoginMethodAvailabilityPolicy([new PhoneChannel()], null!, null!));
        var service = new LoginLinkService(guard, null!, new Tokens(), signIn, fixture.Validator, null!, fixture.TimeProvider, fixture.Logger);
        var result = await service.PreviewAsync(new PreviewLoginLinkRequest(new string('a', 43)), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure); Assert.Equal(LoginLinkErrors.InvalidCode, result.Error.Code); Assert.Equal(2, repo.Reads);
    }
    private sealed class Tokens : ISecureTokenGenerator
    { public string Generate() => throw new NotSupportedException(); public string Hash(string token) => "hash"; }
    private sealed class Links : ILoginLinkRepository
    {
        internal LoginLinkSnapshot? Snapshot { get; set; } internal int Reads { get; private set; }
        public Task<LoginLinkSnapshot?> FindAsync(string hash, CancellationToken ct) { Reads++; return Task.FromResult(Snapshot); }
        public Task<LoginLinkOwnerSnapshot?> FindOwnerAsync(Guid userId, Guid methodId, CancellationToken ct) => throw new NotSupportedException();
        public Task<LoginLink?> GetAsync(string hash, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<LoginLink>> ListPendingAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<DateTime>> ListIssueTimesSinceAsync(Guid userId, DateTime sinceUtc, CancellationToken ct) => throw new NotSupportedException();
        public void Add(LoginLink link) => throw new NotSupportedException();
    }
    private sealed class SignIn(Action mutate) : ISignInService
    {
        public Task<bool> IsLockedOutAsync(Guid userId, CancellationToken ct) { mutate(); return Task.FromResult(false); }
        public Task RegisterFailedAttemptAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();
        public Task ResetFailedAttemptsAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeSessionsAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();
        public Task SignInAsync(Guid userId, Access access, CancellationToken ct) => throw new NotSupportedException();
        public Task<ExternalLogin?> GetExternalLoginAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task SignOutExternalAsync(CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class PhoneChannel : ILoginCodeChannel
    {
        public string Key => "phone"; public LoginMethodType SupportedMethodType => LoginMethodType.Phone;
        public bool IsAvailable => true; public IReadOnlyList<string> Countries => []; public string? ContactPhone => null;
        public Result ValidateNewDestination(LoginCodeDestination destination, string? country) => throw new NotSupportedException();
        public string RenderLoginCode(string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw new NotSupportedException();
        public void EnqueueRenderedLoginCode(string payload, Guid? userId = null) => throw new NotSupportedException();
        public void EnqueueSignup(string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw new NotSupportedException();
        public void EnqueueVerification(Guid? userId, string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw new NotSupportedException();
    }
}
