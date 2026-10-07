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
    /// <summary>Cuándo quedó en la auditoría que venció sin aceptarse. Es técnico: la entrada lleva su propia fecha.</summary>
    [NotAudited]
    public DateTime? ExpiryRecordedAtUtc { get; private set; }
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
        return CheckPending(nowUtc);
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

    public Result CheckPending(DateTime nowUtc)
    {
        RequireUtc(nowUtc);
        if (Status == InvitationStatus.Accepted) return InvitationErrors.AlreadyUsed;
        if (Status == InvitationStatus.Revoked) return InvitationErrors.Invalid;
        return nowUtc >= ExpiresAtUtc ? InvitationErrors.Expired : Result.Success();
    }

    /// <summary>El caso de uso llama sólo después de consumir el código del mismo correo.</summary>
    public Result AcceptVerifiedEmail(Guid userId, Email verifiedEmail, DateTime nowUtc)
    {
        if (userId == Guid.Empty) throw new ArgumentException("A user ID is required.", nameof(userId));
        if (Destination != verifiedEmail) return InvitationErrors.WrongAccount;
        var pending = CheckPending(nowUtc);
        if (pending.IsFailure) return pending;
        Status = InvitationStatus.Accepted;
        AcceptedAtUtc = nowUtc;
        AcceptedByUserId = userId;
        return Result.Success();
    }

    public bool CanBootstrap(string nonceHash, DateTime nowUtc)
        => CanBootstrap(Status, AcceptedAtUtc, BootstrapNonceHash, nonceHash, nowUtc);

    public static bool CanBootstrap(InvitationStatus status, DateTime? acceptedAtUtc, string? expectedNonceHash,
        string nonceHash, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(nonceHash);
        RequireUtc(nowUtc);
        return status == InvitationStatus.Accepted && acceptedAtUtc is { } acceptedAt
            && nowUtc >= acceptedAt && nowUtc < acceptedAt.AddMinutes(5)
            && expectedNonceHash is { } expectedHash && HashEquals(expectedHash, nonceHash);
    }

    public Result Revoke(DateTime nowUtc)
    {
        RequireUtc(nowUtc);
        if (Status == InvitationStatus.Accepted) return InvitationErrors.AlreadyUsed;
        Status = InvitationStatus.Revoked;
        RevokedAtUtc ??= nowUtc;
        return Result.Success();
    }

    /// <summary>
    /// Deja constancia, una sola vez, de que venció sin aceptarse: estaba pendiente al vencer, aunque después se la
    /// haya revocado (reenviarla la revoca). No cambia el estado: «vencida» sigue saliendo de la fecha.
    /// </summary>
    /// <returns>true si había que registrarlo; false si todavía no venció, no venció pendiente o ya estaba registrado.</returns>
    public bool RecordExpiry(DateTime nowUtc)
    {
        RequireUtc(nowUtc);
        if (ExpiryRecordedAtUtc is not null || nowUtc < ExpiresAtUtc) return false;
        var expiredWhilePending = Status switch
        {
            InvitationStatus.Pending => true,
            InvitationStatus.Revoked => RevokedAtUtc is { } revokedAtUtc && revokedAtUtc >= ExpiresAtUtc,
            _ => false,
        };
        if (!expiredWhilePending) return false;
        ExpiryRecordedAtUtc = nowUtc;
        return true;
    }

    public override string ToString() => $"Invitation {{ Id = {Id}, Status = {Status} }}";

    private static bool HashEquals(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));

    private static void RequireUtc(DateTime instant)
    {
        if (instant.Kind != DateTimeKind.Utc) throw new ArgumentException("The instant must be UTC.", nameof(instant));
    }
}
