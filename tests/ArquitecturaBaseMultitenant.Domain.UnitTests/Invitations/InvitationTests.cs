using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Invitations;

/// <summary>Verifica vigencia, uso único y recuperación limitada de una invitación sin consumirla al consultar.</summary>
public sealed class InvitationTests
{
    private static readonly DateTime IssuedAtUtc = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Preview_does_not_consume_and_acceptance_can_only_happen_once()
    {
        var invitation = Issue();
        var userId = Guid.CreateVersion7();

        Assert.True(invitation.CheckToken("hash", IssuedAtUtc).IsSuccess);
        Assert.True(invitation.CheckToken("hash", IssuedAtUtc.AddDays(1)).IsSuccess);
        Assert.Equal(InvitationStatus.Pending, invitation.Status);
        Assert.Null(invitation.AcceptedAtUtc);
        Assert.True(invitation.Accept(userId, "hash", IssuedAtUtc.AddDays(1)).IsSuccess);
        Assert.Equal(InvitationStatus.Accepted, invitation.Status);
        Assert.Equal(userId, invitation.AcceptedByUserId);
        Assert.Equal(InvitationErrors.AlreadyUsed,
            invitation.Accept(userId, "hash", IssuedAtUtc.AddDays(2)).Error);
    }

    [Fact]
    public void Wrong_hash_cannot_consume_or_disclose_an_expired_invitation()
    {
        var invitation = Issue();

        Assert.Equal(InvitationErrors.Invalid,
            invitation.CheckToken("wrong", IssuedAtUtc.AddDays(8)).Error);
        Assert.Equal(InvitationErrors.Invalid,
            invitation.Accept(Guid.CreateVersion7(), "wrong", IssuedAtUtc).Error);
        Assert.Equal(InvitationStatus.Pending, invitation.Status);
    }

    [Fact]
    public void Invitation_expires_at_the_exact_boundary()
    {
        var invitation = Issue();

        Assert.True(invitation.CheckToken("hash", IssuedAtUtc.AddDays(7).AddTicks(-1)).IsSuccess);
        Assert.Equal(InvitationErrors.Expired,
            invitation.CheckToken("hash", IssuedAtUtc.AddDays(7)).Error);
        Assert.Equal(InvitationErrors.Expired,
            invitation.Accept(Guid.CreateVersion7(), "hash", IssuedAtUtc.AddDays(7)).Error);
        Assert.Null(invitation.AcceptedAtUtc);
    }

    [Fact]
    public void Revocation_prevents_acceptance_and_preserves_its_first_instant()
    {
        var invitation = Issue();

        Assert.True(invitation.Revoke(IssuedAtUtc.AddHours(1)).IsSuccess);
        Assert.True(invitation.Revoke(IssuedAtUtc.AddHours(2)).IsSuccess);
        Assert.Equal(IssuedAtUtc.AddHours(1), invitation.RevokedAtUtc);
        Assert.Equal(InvitationErrors.Invalid,
            invitation.Accept(Guid.CreateVersion7(), "hash", IssuedAtUtc.AddHours(3)).Error);
    }

    [Fact]
    public void Bootstrap_requires_the_original_browser_and_expires_after_five_minutes()
    {
        var invitation = Issue();

        Assert.False(invitation.CanBootstrap("browser-hash", IssuedAtUtc));
        Assert.True(invitation.Accept(Guid.CreateVersion7(), "hash", IssuedAtUtc, "browser-hash").IsSuccess);
        Assert.True(invitation.CanBootstrap("browser-hash", IssuedAtUtc.AddMinutes(5).AddTicks(-1)));
        Assert.False(invitation.CanBootstrap("other-browser", IssuedAtUtc.AddMinutes(1)));
        Assert.False(invitation.CanBootstrap("browser-hash", IssuedAtUtc.AddMinutes(5)));
    }

    [Fact]
    public void Accepting_an_existing_account_does_not_grant_bootstrap()
    {
        var invitation = Issue();

        Assert.True(invitation.Accept(Guid.CreateVersion7(), "hash", IssuedAtUtc).IsSuccess);
        Assert.False(invitation.CanBootstrap("hash", IssuedAtUtc));
        Assert.Null(invitation.BootstrapNonceHash);
    }

    [Fact]
    public void Diagnostic_text_excludes_recipient_hash_and_browser_nonce()
    {
        var invitation = Issue();
        Assert.True(invitation.Accept(Guid.CreateVersion7(), "hash", IssuedAtUtc, "browser-hash").IsSuccess);

        var diagnostic = invitation.ToString();
        Assert.DoesNotContain("ana@example.test", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("hash", diagnostic, StringComparison.Ordinal);
        Assert.Equal(Guid.Empty, invitation.TenantId);
    }

    private static Invitation Issue() => Invitation.Issue(Guid.CreateVersion7(), Guid.CreateVersion7(),
        Email.Create("ANA@example.test").Value, InvitationChannel.Email, "hash", IssuedAtUtc, TimeSpan.FromDays(7));
}
