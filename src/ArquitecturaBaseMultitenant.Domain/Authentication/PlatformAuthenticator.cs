using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>
/// El autenticador de un operador (E9): la clave TOTP protegida con Data Protection y el último intervalo aceptado.
/// Un intervalo ya consumido no vuelve a servir, aunque el código siga dentro de la tolerancia.
/// </summary>
public sealed class PlatformAuthenticator : Entity
{
    private PlatformAuthenticator() { }

    public Guid UserId { get; private set; }
    public byte[] ProtectedSecret { get; private set; } = [];
    public long LastAcceptedTimeStep { get; private set; }
    public DateTime ConfiguredAtUtc { get; private set; }

    /// <summary>El alta terminada: conserva la clave verificada y el intervalo con el que se verificó.</summary>
    public static PlatformAuthenticator Configure(Guid userId, byte[] protectedSecret, long acceptedTimeStep,
        DateTime nowUtc)
    {
        if (userId == Guid.Empty) throw new ArgumentException("The user id cannot be empty.", nameof(userId));
        if (protectedSecret is not { Length: > 0 })
            throw new ArgumentException("A protected secret is required.", nameof(protectedSecret));
        ArgumentOutOfRangeException.ThrowIfNegative(acceptedTimeStep);
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("The instant must be UTC.", nameof(nowUtc));
        return new PlatformAuthenticator
        {
            UserId = userId,
            ProtectedSecret = protectedSecret,
            LastAcceptedTimeStep = acceptedTimeStep,
            ConfiguredAtUtc = nowUtc,
        };
    }

    /// <summary>Consume el intervalo del código: sólo uno posterior al último aceptado.</summary>
    public Result Accept(long timeStep)
    {
        if (timeStep <= LastAcceptedTimeStep) return TwoFactorErrors.Invalid;
        LastAcceptedTimeStep = timeStep;
        return Result.Success();
    }
}
