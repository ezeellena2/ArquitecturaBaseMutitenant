using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>El desafío pendiente como lo lee el navegador (E9); el material protegido no se muestra en logs.</summary>
public sealed record PlatformFactorChallengeRow(Guid Id, Guid UserId, string BindingHash, string SecurityStamp,
    string ReturnUrl, PlatformFactorStep Step, byte[]? ProtectedSecret, byte[]? ProtectedRecoveryCodes,
    DateTime ExpiresAtUtc, DateTime? CompletedAtUtc, DateTime? InvalidatedAtUtc)
{
    public bool IsActive(DateTime nowUtc) => CompletedAtUtc is null && InvalidatedAtUtc is null && nowUtc < ExpiresAtUtc;

    public override string ToString() => nameof(PlatformFactorChallengeRow);
}

/// <summary>Una sesión de Plataforma (E9) con el sello con que nació y su prueba reciente.</summary>
public sealed record PlatformSessionRow(Guid Id, Guid UserId, string SecurityStamp, DateTime StartedAtUtc,
    DateTime RecentProofUntilUtc, DateTime? RevokedAtUtc)
{
    public bool IsValid(string currentSecurityStamp) =>
        RevokedAtUtc is null && string.Equals(SecurityStamp, currentSecurityStamp, StringComparison.Ordinal);

    public override string ToString() => nameof(PlatformSessionRow);
}

/// <summary>El vínculo durable de una operación del segundo factor (E9).</summary>
public sealed record PlatformFactorOperationRow(Guid Id, Guid IdempotencyKey, PlatformFactorOperationKind Kind,
    Guid UserId, Guid? ChallengeId, Guid? PlatformSessionId, string BodyHash, string Route, string? ReturnUrl,
    byte[]? ProtectedSessionId, bool ShowsRecoveryCodes, DateTime? VerifiedAtUtc, DateTime? RecentUntilUtc)
{
    public bool Matches(string bodyHash, string route) =>
        string.Equals(BodyHash, bodyHash, StringComparison.Ordinal) && string.Equals(Route, route, StringComparison.Ordinal);

    public override string ToString() => nameof(PlatformFactorOperationRow);
}
