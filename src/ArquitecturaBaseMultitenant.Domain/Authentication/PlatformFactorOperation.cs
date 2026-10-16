using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>
/// El vínculo durable de una operación del segundo factor con su clave de idempotencia (E9). Se confirma en la misma
/// transacción que consume el código, crea la sesión o deja la prueba reciente, y conserva lo justo para reconstruir la
/// respuesta: el retorno, la sesión creada con su identificador protegido o el vencimiento original de la prueba. El
/// almacén global de idempotencia sólo guarda la referencia técnica a esta fila.
/// </summary>
public sealed class PlatformFactorOperation : Entity
{
    private PlatformFactorOperation() { }

    public Guid IdempotencyKey { get; private set; }
    public PlatformFactorOperationKind Kind { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? ChallengeId { get; private set; }
    public Guid? PlatformSessionId { get; private set; }
    public string BodyHash { get; private set; } = string.Empty;
    public string Route { get; private set; } = string.Empty;
    public string? ReturnUrl { get; private set; }
    public byte[]? ProtectedSessionId { get; private set; }
    public bool ShowsRecoveryCodes { get; private set; }
    public DateTime? VerifiedAtUtc { get; private set; }
    public DateTime? RecentUntilUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Verificar o completar terminó el ingreso: la sesión de Plataforma ya existe.</summary>
    public static PlatformFactorOperation ForSignIn(Guid idempotencyKey, PlatformFactorOperationKind kind, Guid userId,
        Guid challengeId, string bodyHash, string route, string returnUrl, Guid platformSessionId,
        byte[] protectedSessionId, DateTime nowUtc)
    {
        if (kind is not (PlatformFactorOperationKind.Verify or PlatformFactorOperationKind.Complete))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (challengeId == Guid.Empty || platformSessionId == Guid.Empty)
            throw new ArgumentException("The challenge and the session are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(returnUrl);
        if (protectedSessionId is not { Length: > 0 })
            throw new ArgumentException("The protected session id is required.", nameof(protectedSessionId));
        var operation = Create(idempotencyKey, kind, userId, bodyHash, route, nowUtc);
        operation.ChallengeId = challengeId;
        operation.PlatformSessionId = platformSessionId;
        operation.ReturnUrl = returnUrl;
        operation.ProtectedSessionId = protectedSessionId;
        return operation;
    }

    /// <summary>El alta quedó verificada: la respuesta pide mostrar los códigos, sin sesión todavía.</summary>
    public static PlatformFactorOperation ForSetupVerified(Guid idempotencyKey, Guid userId, Guid challengeId,
        string bodyHash, string route, DateTime nowUtc)
    {
        if (challengeId == Guid.Empty) throw new ArgumentException("The challenge is required.", nameof(challengeId));
        var operation = Create(idempotencyKey, PlatformFactorOperationKind.Verify, userId, bodyHash, route, nowUtc);
        operation.ChallengeId = challengeId;
        operation.ShowsRecoveryCodes = true;
        return operation;
    }

    /// <summary>La prueba reciente quedó ligada a la sesión; el replay conserva su vencimiento original.</summary>
    public static PlatformFactorOperation ForReauthentication(Guid idempotencyKey, Guid userId, Guid platformSessionId,
        string bodyHash, string route, DateTime verifiedAtUtc, DateTime recentUntilUtc, DateTime nowUtc)
    {
        if (platformSessionId == Guid.Empty) throw new ArgumentException("The session is required.", nameof(platformSessionId));
        if (verifiedAtUtc.Kind != DateTimeKind.Utc || recentUntilUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The proof instants must be UTC.");
        var operation = Create(idempotencyKey, PlatformFactorOperationKind.Reauthenticate, userId, bodyHash, route, nowUtc);
        operation.PlatformSessionId = platformSessionId;
        operation.VerifiedAtUtc = verifiedAtUtc;
        operation.RecentUntilUtc = recentUntilUtc;
        return operation;
    }

    /// <summary>La misma operación: misma clave con el mismo cuerpo y la misma ruta.</summary>
    public bool Matches(string bodyHash, string route) =>
        string.Equals(BodyHash, bodyHash, StringComparison.Ordinal) && string.Equals(Route, route, StringComparison.Ordinal);

    private static PlatformFactorOperation Create(Guid idempotencyKey, PlatformFactorOperationKind kind, Guid userId,
        string bodyHash, string route, DateTime nowUtc)
    {
        if (idempotencyKey == Guid.Empty) throw new ArgumentException("The idempotency key cannot be empty.", nameof(idempotencyKey));
        if (userId == Guid.Empty) throw new ArgumentException("The user id cannot be empty.", nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(bodyHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("The instant must be UTC.", nameof(nowUtc));
        return new PlatformFactorOperation
        {
            IdempotencyKey = idempotencyKey,
            Kind = kind,
            UserId = userId,
            BodyHash = bodyHash,
            Route = route,
            CreatedAtUtc = nowUtc,
        };
    }
}
