using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>Un código de recuperación de un operador (E9): sólo su hash con clave, de un solo uso.</summary>
public sealed class PlatformRecoveryCode : Entity
{
    private PlatformRecoveryCode() { }

    public Guid UserId { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UsedAtUtc { get; private set; }

    public static PlatformRecoveryCode Issue(Guid userId, string codeHash, DateTime nowUtc)
    {
        if (userId == Guid.Empty) throw new ArgumentException("The user id cannot be empty.", nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(codeHash);
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("The instant must be UTC.", nameof(nowUtc));
        return new PlatformRecoveryCode { UserId = userId, CodeHash = codeHash, CreatedAtUtc = nowUtc };
    }

    public Result Use(DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("The instant must be UTC.", nameof(nowUtc));
        if (UsedAtUtc is not null) return TwoFactorErrors.RecoveryInvalid;
        UsedAtUtc = nowUtc;
        return Result.Success();
    }
}
