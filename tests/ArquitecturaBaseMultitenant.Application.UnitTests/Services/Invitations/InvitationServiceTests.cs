using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Services.Invitations;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Invitations;

public sealed class InvitationServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(null)]
    [InlineData("tampered")]
    public async Task Missing_or_tampered_token_never_enters_a_tenant_scope(string? token)
    {
        using var setup = new Setup();

        var preview = await setup.Service.PreviewAsync(new PreviewInvitationRequest(token), Ct);

        Assert.Equal(InvitationPreviewState.Invalid, preview.Value.State);
        Assert.Null(preview.Value.Email);
        Assert.Equal(0, setup.Ports.Entries);
        Assert.Equal(0, setup.Ports.Reads);
    }

    [Theory]
    [InlineData("none", InvitationPreviewState.NoAccount)]
    [InlineData("existing", InvitationPreviewState.SignInRequired)]
    [InlineData("same", InvitationPreviewState.Ready)]
    [InlineData("other", InvitationPreviewState.WrongSession)]
    public async Task Preview_distinguishes_account_and_session_without_consuming_invitation(
        string session, InvitationPreviewState expected)
    {
        using var setup = new Setup();
        var owner = Guid.CreateVersion7();
        if (session != "none") setup.Ports.Owners = [owner];
        setup.Ports.UserId = session switch { "same" => owner, "other" => Guid.CreateVersion7(), _ => null };

        var preview = await setup.Service.PreviewAsync(new PreviewInvitationRequest("valid"), Ct);

        Assert.Equal(expected, preview.Value.State);
        Assert.Equal(setup.Ports.Row.Destination, preview.Value.Email);
        Assert.Equal(InvitationStatus.Pending, setup.Ports.Row.Status);
        Assert.NotNull(setup.Ports.Continuation);
        Assert.Equal(1, setup.Ports.Entries);
        Assert.Equal(0, setup.Ports.SignIns);
    }

    [Theory]
    [InlineData("expired", InvitationPreviewState.Expired)]
    [InlineData("revoked", InvitationPreviewState.Invalid)]
    [InlineData("accepted", InvitationPreviewState.Invalid)]
    [InlineData("suspended", InvitationPreviewState.OrganizationSuspended)]
    [InlineData("removed", InvitationPreviewState.Invalid)]
    [InlineData("bad-hash", InvitationPreviewState.Invalid)]
    public async Task Preview_checks_current_private_state_and_exact_expiry(string state, InvitationPreviewState expected)
    {
        using var setup = new Setup();
        setup.Ports.Row = state switch
        {
            "expired" => setup.Ports.Row with { ExpiresAtUtc = setup.Fixture.TimeProvider.GetUtcNow().UtcDateTime },
            "revoked" => setup.Ports.Row with { Status = InvitationStatus.Revoked },
            "accepted" => setup.Ports.Row with { Status = InvitationStatus.Accepted },
            "suspended" => setup.Ports.Row with { TenantStatus = TenantStatus.Suspended },
            "removed" => setup.Ports.Row with { MemberStatus = MemberStatus.Removed },
            _ => setup.Ports.Row with { TokenHash = "different" },
        };

        var preview = await setup.Service.PreviewAsync(new PreviewInvitationRequest("valid"), Ct);

        Assert.Equal(expected, preview.Value.State);
        Assert.Equal(0, setup.Ports.SignIns);
        if (expected == InvitationPreviewState.Invalid) Assert.Null(preview.Value.Email);
    }

    [Fact]
    public async Task Reload_uses_the_protected_continuation_and_keeps_its_browser_nonce()
    {
        using var setup = new Setup();
        var first = await setup.Service.PreviewAsync(new PreviewInvitationRequest("valid"), Ct);
        var nonce = setup.Ports.Continuation!.Nonce;

        var reloaded = await setup.Service.PreviewAsync(new PreviewInvitationRequest(null), Ct);

        Assert.Equal(first.Value, reloaded.Value);
        Assert.Equal(nonce, setup.Ports.Continuation!.Nonce);
        Assert.Equal(0, setup.Ports.SignIns);
    }

    [Fact]
    public async Task Deleted_inviter_has_the_resource_name_and_never_the_old_personal_name()
    {
        using var setup = new Setup();
        setup.Ports.Row = setup.Ports.Row with { InviterName = "Nombre anterior", InviterDeleted = true };

        var preview = await setup.Service.PreviewAsync(new PreviewInvitationRequest("valid"), Ct);

        Assert.Equal(ErrorTexts.Get("Legal.AccountDeletion.DeletedDisplayName"), preview.Value.InviterName);
    }

    [Fact]
    public async Task An_explicit_new_token_replaces_the_old_continuation_and_its_nonce()
    {
        using var setup = new Setup();
        setup.Ports.Continuation = new InvitationContinuation("older-token", "older-nonce", setup.Ports.Row.ExpiresAtUtc);

        var preview = await setup.Service.PreviewAsync(new PreviewInvitationRequest("valid"), Ct);

        Assert.Equal(InvitationPreviewState.NoAccount, preview.Value.State);
        Assert.Equal("valid", setup.Ports.Continuation!.Token);
        Assert.NotEqual("older-nonce", setup.Ports.Continuation.Nonce);
    }

    private sealed class Setup : IDisposable
    {
        public ServiceFixture<InvitationService> Fixture { get; } = new();
        public Ports Ports { get; }
        public InvitationService Service { get; }
        public Setup()
        {
            Ports = new Ports(Fixture.TimeProvider.GetUtcNow().UtcDateTime);
            var availability = new LoginMethodAvailability([], Ports, Ports);
            var previews = new InvitationPreviewBuilder(Ports, Ports, Ports, availability, Ports, Fixture.TimeProvider, Ports);
            Service = new InvitationService(new InvitationPreviewFlow(Ports, Ports, previews, Ports, Fixture.TimeProvider),
                null!, null!, Fixture.Validator, new FakeUnitOfWork(), null!, Fixture.TimeProvider, Fixture.Logger);
        }
        public void Dispose() => Fixture.Dispose();
    }

    private sealed class Ports : IInvitationReader, IInvitationTokenProtector, ITenantScope,
        IInvitationFlowContext, IUserLookup, ILoginMethodReader, IGoogleAvailability, ILoginMethodMembershipReader
    {
        public Ports(DateTime nowUtc) => Row = new InvitationRow(Guid.CreateVersion7(), Guid.CreateVersion7(), null,
            MemberStatus.Invited, Guid.CreateVersion7(), TenantKind.Business, TenantStatus.Active, "Organización real",
            Email.Create("recipient@example.test").Value, InvitationChannel.Email, "hash:secret", InvitationStatus.Pending,
            nowUtc, nowUtc.AddDays(7), null, null, null, "Invitador real");
        public InvitationRow Row { get; set; }
        public Guid? UserId { get; set; }
        public IReadOnlyList<Guid> Owners { get; set; } = [];
        public InvitationContinuation? Continuation { get; set; }
        public int Entries { get; private set; }
        public int Reads { get; private set; }
        public int SignIns { get; private set; }
        public bool IsEnabled => true;
        public Task<InvitationRow?> FindByIdAsync(Guid id, CancellationToken ct)
        { Reads++; return Task.FromResult<InvitationRow?>(Row); }
        public string GenerateSecret() => "nonce";
        public string Hash(string secret) => "hash:" + secret;
        public string Protect(InvitationTokenData data) => "valid";
        public InvitationTokenData? Unprotect(string token) => token == "valid" ? new(Row.TenantId, Row.Id, "secret") : null;
        public IDisposable Enter(Guid tenantId) { Assert.Equal(Row.TenantId, tenantId); Entries++; return new Scope(); }
        public Task<Guid?> GetUserIdAsync(CancellationToken ct) => Task.FromResult(UserId);
        public InvitationContinuation? ReadContinuation() => Continuation;
        public void Remember(InvitationContinuation continuation) => Continuation = continuation;
        public void ClearContinuation() => Continuation = null;
        public Task<bool> RestoreSessionAsync(Guid userId, CancellationToken ct) { SignIns++; UserId = userId; return Task.FromResult(true); }
        public Task<IReadOnlyList<Guid>> FindVerifiedUsersByEmailAsync(Email email, CancellationToken ct) => Task.FromResult(Owners);
        public Task<Guid?> FindVerifiedUserIdAsync(LoginMethodType type, string value, CancellationToken ct) => Task.FromResult<Guid?>(null);
        public Task<LoginMethodLookup?> FindMethodAsync(LoginMethodType type, string value, CancellationToken ct) => Task.FromResult<LoginMethodLookup?>(null);
        public Task<IReadOnlyList<LoginMethodRow>> ListByUserIdAsync(Guid userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<LoginMethodRow>>([]);
        public Task<bool> IsActiveAsync(Guid userId, Guid tenantId, CancellationToken ct) => Task.FromResult(true);
        private sealed class Scope : IDisposable { public void Dispose() { } }
    }
}
