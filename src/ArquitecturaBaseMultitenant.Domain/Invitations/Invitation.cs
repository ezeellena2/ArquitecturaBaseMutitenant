using System.Security.Cryptography;
using System.Text;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.Invitations;

/// <summary>Invitación privada, de un solo uso. Conserva hashes y nunca el token enviado al destinatario.</summary>
public sealed class Invitation : Entity, ITenantOwned, IAuditable
{
    private Invitation()
    {
        Destination = null!;
        Channel = string.Empty;
        TokenHash = string.Empty;
    }

    public Guid TenantId { get; private set; }
    public Guid MemberId { get; private set; }
    public Guid InviterUserId { get; private set; }
    [NotAudited]
    public Email Destination { get; private set; }
    public string Channel { get; private set; }
    [NotAudited]
    public string TokenHash { get; private set; }
    public InvitationStatus Status { get; private set; }
    public DateTime IssuedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? AcceptedAtUtc { get; private set; }
    public Guid? AcceptedByUserId { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    [NotAudited]
    public string? BootstrapNonceHash { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }

    public static Invitation Issue(Guid memberId, Guid inviterUserId, Email destination, string channel,
        string tokenHash, DateTime nowUtc, TimeSpan lifetime)
    {
        if (memberId == Guid.Empty) throw new ArgumentException("A member ID is required.", nameof(memberId));
        if (inviterUserId == Guid.Empty) throw new ArgumentException("An inviter ID is required.", nameof(inviterUserId));
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);
        RequireUtc(nowUtc);

        return new Invitation
        {
            MemberId = memberId,
            InviterUserId = inviterUserId,
            Destination = destination,
            Channel = channel,
            TokenHash = tokenHash,
            Status = InvitationStatus.Pending,
            IssuedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc + lifetime,
        };
    }

    public Result CheckToken(string tokenHash, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);
        RequireUtc(nowUtc);
        if (!HashEquals(TokenHash, tokenHash)) return InvitationErrors.Invalid;
        if (Status == InvitationStatus.Accepted) return InvitationErrors.AlreadyUsed;
        if (Status == InvitationStatus.Revoked) return InvitationErrors.Invalid;
        return nowUtc >= ExpiresAtUtc ? InvitationErrors.Expired : Result.Success();
    }

    public Result Accept(Guid userId, string tokenHash, DateTime nowUtc, string? bootstrapNonceHash = null)
    {
        if (userId == Guid.Empty) throw new ArgumentException("A user ID is required.", nameof(userId));
        var checkedToken = CheckToken(tokenHash, nowUtc);
        if (checkedToken.IsFailure) return checkedToken;

        Status = InvitationStatus.Accepted;
        AcceptedAtUtc = nowUtc;
        AcceptedByUserId = userId;
        BootstrapNonceHash = bootstrapNonceHash;
        return Result.Success();
    }

    public bool CanBootstrap(string nonceHash, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(nonceHash);
        RequireUtc(nowUtc);
        return Status == InvitationStatus.Accepted && AcceptedAtUtc is { } acceptedAt
            && nowUtc >= acceptedAt && nowUtc < acceptedAt.AddMinutes(5)
            && BootstrapNonceHash is { } expectedHash && HashEquals(expectedHash, nonceHash);
    }

    public Result Revoke(DateTime nowUtc)
    {
        RequireUtc(nowUtc);
        if (Status == InvitationStatus.Accepted) return InvitationErrors.AlreadyUsed;
        Status = InvitationStatus.Revoked;
        RevokedAtUtc ??= nowUtc;
        return Result.Success();
    }

    public override string ToString() => $"Invitation {{ Id = {Id}, Status = {Status} }}";

    private static bool HashEquals(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));

    private static void RequireUtc(DateTime instant)
    {
        if (instant.Kind != DateTimeKind.Utc) throw new ArgumentException("The instant must be UTC.", nameof(instant));
    }
}
