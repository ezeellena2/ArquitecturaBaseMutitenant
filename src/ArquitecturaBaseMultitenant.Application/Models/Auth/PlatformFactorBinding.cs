namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>
/// El vínculo del navegador con su desafío pendiente (E9): el id del desafío y el token cuyo hash guarda la base. Va
/// sólo en la cookie protegida; nunca se registra.
/// </summary>
public sealed record PlatformFactorBinding(Guid ChallengeId, string Token)
{
    public override string ToString() => nameof(PlatformFactorBinding);
}

/// <summary>
/// La sesión de Plataforma recién creada en la transacción del segundo factor: su id, el identificador de la cookie
/// (sólo para emitirla después del commit) y el momento de inicio.
/// </summary>
public sealed record IssuedPlatformSession(Guid PlatformSessionId, Guid UserId, string SessionId, DateTime StartedAtUtc)
{
    public override string ToString() => nameof(IssuedPlatformSession);
}

/// <summary>Lo que muestra el alta del autenticador: la clave base32 para copiar y el otpauth del QR.</summary>
public sealed record PlatformAuthenticatorSetup(string Secret, string OtpauthUri)
{
    public override string ToString() => nameof(PlatformAuthenticatorSetup);
}
