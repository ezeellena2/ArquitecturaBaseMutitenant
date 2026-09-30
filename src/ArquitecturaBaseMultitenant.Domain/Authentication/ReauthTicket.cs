using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>Comprobante de un solo uso. Persiste solo el hash del secreto y el contexto de la acción.</summary>
public sealed class ReauthTicket : Entity
{
    private ReauthTicket() { }

    public Guid UserId { get; private set; }
    public ReauthAction Action { get; private set; }
    public Guid? SourceMethodId { get; private set; }
    public Guid? TargetMethodId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime IssuedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }
    public string? ReturnUrl { get; private set; }

    public static Result<ReauthTicket> Issue(Guid userId, ReauthAction action, Guid? sourceMethodId,
        Guid? targetMethodId, string tokenHash, DateTime issuedAtUtc, string? returnUrl = null)
    {
        if (userId == Guid.Empty) throw new ArgumentException("A user is required.", nameof(userId));
        if (!Enum.IsDefined(action)) throw new ArgumentOutOfRangeException(nameof(action));
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        RequireUtc(issuedAtUtc);
        if (action is ReauthAction.RemoveMethod or ReauthAction.MakePrimary
            && (sourceMethodId is null || targetMethodId is null || sourceMethodId == targetMethodId))
            return ReauthErrors.OtherMethodRequired;

        return new ReauthTicket
        {
            UserId = userId, Action = action, SourceMethodId = sourceMethodId,
            TargetMethodId = targetMethodId, TokenHash = tokenHash, IssuedAtUtc = issuedAtUtc,
            ExpiresAtUtc = issuedAtUtc.AddMinutes(5), ReturnUrl = returnUrl,
        };
    }

    public Result Consume(Guid userId, ReauthAction action, Guid? targetMethodId, DateTime nowUtc)
    {
        RequireUtc(nowUtc);
        if (ConsumedAtUtc is not null || UserId != userId || Action != action || TargetMethodId != targetMethodId)
            return ReauthErrors.Invalid;
        if (nowUtc < IssuedAtUtc || nowUtc >= ExpiresAtUtc) return ReauthErrors.Expired;
        ConsumedAtUtc = nowUtc;
        return Result.Success();
    }

    public override string ToString() => nameof(ReauthTicket);

    private static void RequireUtc(DateTime instantUtc)
    {
        if (instantUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The instant must be UTC.", nameof(instantUtc));
    }
}

