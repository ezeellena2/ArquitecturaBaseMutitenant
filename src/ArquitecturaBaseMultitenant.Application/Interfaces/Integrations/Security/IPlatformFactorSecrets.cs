using ArquitecturaBaseMultitenant.Application.Models.Auth;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;

/// <summary>
/// El material secreto del segundo factor de un operador (E9): la clave TOTP protegida con Data Protection, el código
/// del autenticador con un intervalo de tolerancia por lado, los códigos de recuperación con su hash con clave y el
/// identificador de sesión que un replay vuelve a emitir. Nada sale de acá en claro salvo lo que la pantalla muestra.
/// </summary>
public interface IPlatformFactorSecrets
{
    /// <summary>Una clave nueva, ya protegida para esa cuenta.</summary>
    byte[] NewProtectedSecret(Guid userId);

    /// <summary>La clave en base32 y el otpauth del QR, para el alta.</summary>
    PlatformAuthenticatorSetup DescribeSetup(Guid userId, byte[] protectedSecret, string accountName);

    /// <summary>El intervalo TOTP que coincide con el código, dentro de un intervalo por lado; null si ninguno.</summary>
    long? MatchTimeStep(Guid userId, byte[] protectedSecret, string code, DateTime nowUtc);

    /// <summary>Los ocho códigos XXXX-XXXX del alta, aleatorios y distintos.</summary>
    IReadOnlyList<string> NewRecoveryCodes();

    string HashRecoveryCode(Guid userId, string normalizedCode);

    byte[] ProtectRecoveryCodes(Guid challengeId, IReadOnlyList<string> codes);

    IReadOnlyList<string> UnprotectRecoveryCodes(Guid challengeId, byte[] protectedCodes);

    byte[] ProtectSessionId(Guid platformSessionId, string sessionId);

    string UnprotectSessionId(Guid platformSessionId, byte[] protectedSessionId);
}
