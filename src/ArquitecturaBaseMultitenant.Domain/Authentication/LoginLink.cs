using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>Prueba de posesión de un método concreto. Sólo persiste el hash, nunca el token o la URL.</summary>
public sealed class LoginLink : Entity
{
    /// <summary>La vida de siempre; el emisor usa la de sus opciones (E9).</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private LoginLink() { }
    public Guid UserId { get; private set; }
    public Guid MethodId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }
    public DateTime? InvalidatedAtUtc { get; private set; }
    public static LoginLink Issue(Guid userId, Guid methodId, string hash, DateTime nowUtc) =>
        Issue(userId, methodId, hash, nowUtc, Lifetime);

    public static LoginLink Issue(Guid userId, Guid methodId, string hash, DateTime nowUtc, TimeSpan lifetime)
    {
        if (userId == Guid.Empty || methodId == Guid.Empty) throw new ArgumentException("Account and method required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);
        RequireUtc(nowUtc);
        return new LoginLink { UserId = userId, MethodId = methodId, TokenHash = hash,
            CreatedAtUtc = nowUtc, ExpiresAtUtc = nowUtc + lifetime };
    }
    public bool IsActive(DateTime nowUtc) => ConsumedAtUtc is null && InvalidatedAtUtc is null && nowUtc < ExpiresAtUtc;
    public void Invalidate(DateTime nowUtc) { RequireUtc(nowUtc); InvalidatedAtUtc ??= nowUtc; }
    public Result Redeem(DateTime nowUtc)
    {
        RequireUtc(nowUtc);
        if (!IsActive(nowUtc)) return LoginLinkErrors.Invalid;
        ConsumedAtUtc = nowUtc;
        return Result.Success();
    }
    private static void RequireUtc(DateTime value)
    { if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required."); }
}
