using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>
/// La sesión de Plataforma de un navegador (E9): nace al completar los dos factores y es la evidencia que piden el
/// authorize y el refresh del operador. Guarda el hash del identificador de la cookie, el sello de seguridad con que
/// nació y la prueba reciente de diez minutos (P1), que comparten las pestañas de esta sesión y ninguna otra.
/// </summary>
public sealed class PlatformSession : Entity
{
    public static readonly TimeSpan RecentProofWindow = TimeSpan.FromMinutes(10);

    private PlatformSession() { }

    public Guid UserId { get; private set; }
    public string SessionIdHash { get; private set; } = string.Empty;
    public string SecurityStamp { get; private set; } = string.Empty;
    public LoginAuditMethod Method { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime RecentProofAtUtc { get; private set; }
    public DateTime RecentProofUntilUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    /// <summary>El ingreso completo cuenta como prueba reciente.</summary>
    public static PlatformSession Start(Guid userId, string sessionIdHash, string securityStamp,
        LoginAuditMethod method, DateTime nowUtc)
    {
        if (userId == Guid.Empty) throw new ArgumentException("The user id cannot be empty.", nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionIdHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(securityStamp);
        if (method is not (LoginAuditMethod.Authenticator or LoginAuditMethod.RecoveryCode))
            throw new ArgumentOutOfRangeException(nameof(method));
        RequireUtc(nowUtc);
        return new PlatformSession
        {
            UserId = userId,
            SessionIdHash = sessionIdHash,
            SecurityStamp = securityStamp,
            Method = method,
            StartedAtUtc = nowUtc,
            RecentProofAtUtc = nowUtc,
            RecentProofUntilUtc = nowUtc + RecentProofWindow,
        };
    }

    /// <summary>Vale mientras no se revoque y la cuenta conserve el sello con que nació.</summary>
    public bool IsValid(string currentSecurityStamp) =>
        RevokedAtUtc is null && string.Equals(SecurityStamp, currentSecurityStamp, StringComparison.Ordinal);

    public bool HasRecentProof(string currentSecurityStamp, DateTime nowUtc)
    {
        RequireUtc(nowUtc);
        return IsValid(currentSecurityStamp) && nowUtc < RecentProofUntilUtc;
    }

    /// <summary>«Confirmá que sos vos»: la prueba vale diez minutos desde el momento del servidor.</summary>
    public void ConfirmRecentProof(DateTime nowUtc)
    {
        RequireUtc(nowUtc);
        RecentProofAtUtc = nowUtc;
        RecentProofUntilUtc = nowUtc + RecentProofWindow;
    }

    public void Revoke(DateTime nowUtc)
    {
        RequireUtc(nowUtc);
        RevokedAtUtc ??= nowUtc;
    }

    private static void RequireUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("The instant must be UTC.", nameof(value));
    }
}
