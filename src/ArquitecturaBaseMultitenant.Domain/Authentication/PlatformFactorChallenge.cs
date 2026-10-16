using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>
/// El segundo factor pendiente de un operador (E9). Nace al probar el primer factor con access=platform, queda ligado
/// al navegador por el hash de su vínculo, al sello de seguridad de la cuenta y al authorize validado, y dura diez
/// minutos sin renovarse. En el alta guarda la clave nueva y, ya verificada, los ocho códigos de recuperación; los dos
/// van protegidos y se destruyen al completar o invalidar el desafío.
/// </summary>
public sealed class PlatformFactorChallenge : Entity
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private PlatformFactorChallenge() { }

    public Guid UserId { get; private set; }
    public string BindingHash { get; private set; } = string.Empty;
    public string SecurityStamp { get; private set; } = string.Empty;
    public string ReturnUrl { get; private set; } = string.Empty;
    public PlatformFactorStep Step { get; private set; }
    public byte[]? ProtectedSecret { get; private set; }
    public long? AcceptedTimeStep { get; private set; }
    public byte[]? ProtectedRecoveryCodes { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? InvalidatedAtUtc { get; private set; }

    /// <summary>El operador ya tiene su autenticador: el desafío pide el código o uno de recuperación.</summary>
    public static PlatformFactorChallenge ForVerify(Guid userId, string bindingHash, string securityStamp,
        string returnUrl, DateTime nowUtc) =>
        Create(userId, bindingHash, securityStamp, returnUrl, PlatformFactorStep.Verify, protectedSecret: null, nowUtc);

    /// <summary>Sin autenticador: el desafío lleva la clave nueva, preparada en la misma transacción del primer factor.</summary>
    public static PlatformFactorChallenge ForSetup(Guid userId, string bindingHash, string securityStamp,
        string returnUrl, byte[] protectedSecret, DateTime nowUtc)
    {
        if (protectedSecret is not { Length: > 0 })
            throw new ArgumentException("A protected secret is required to set up the authenticator.", nameof(protectedSecret));
        return Create(userId, bindingHash, securityStamp, returnUrl, PlatformFactorStep.Setup, protectedSecret, nowUtc);
    }

    public bool IsActive(DateTime nowUtc)
    {
        RequireUtc(nowUtc);
        return CompletedAtUtc is null && InvalidatedAtUtc is null && nowUtc < ExpiresAtUtc;
    }

    /// <summary>El alta quedó verificada con el autenticador: guarda el intervalo usado y los códigos protegidos.</summary>
    public Result ConfirmSetup(long acceptedTimeStep, byte[] protectedRecoveryCodes)
    {
        if (protectedRecoveryCodes is not { Length: > 0 })
            throw new ArgumentException("The protected recovery codes are required.", nameof(protectedRecoveryCodes));
        if (Step != PlatformFactorStep.Setup) return TwoFactorErrors.InvalidState;
        AcceptedTimeStep = acceptedTimeStep;
        ProtectedRecoveryCodes = protectedRecoveryCodes;
        Step = PlatformFactorStep.RecoveryCodes;
        return Result.Success();
    }

    /// <summary>
    /// Termina el ingreso desde el paso esperado: Verify con el factor probado, o RecoveryCodes con los códigos
    /// guardados. Destruye la clave y los códigos temporales.
    /// </summary>
    public Result Complete(PlatformFactorStep expected, DateTime nowUtc)
    {
        if (!IsActive(nowUtc)) return TwoFactorErrors.ChallengeExpired;
        if (Step != expected || expected == PlatformFactorStep.Setup) return TwoFactorErrors.InvalidState;
        CompletedAtUtc = nowUtc;
        DestroyMaterial();
        return Result.Success();
    }

    /// <summary>Un ingreso nuevo, un restablecimiento o un cierre de sesiones dejan el desafío sin valor.</summary>
    public void Invalidate(DateTime nowUtc)
    {
        RequireUtc(nowUtc);
        InvalidatedAtUtc ??= nowUtc;
        DestroyMaterial();
    }

    private void DestroyMaterial()
    {
        ProtectedSecret = null;
        ProtectedRecoveryCodes = null;
    }

    private static PlatformFactorChallenge Create(Guid userId, string bindingHash, string securityStamp,
        string returnUrl, PlatformFactorStep step, byte[]? protectedSecret, DateTime nowUtc)
    {
        if (userId == Guid.Empty) throw new ArgumentException("The user id cannot be empty.", nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(securityStamp);
        ArgumentException.ThrowIfNullOrWhiteSpace(returnUrl);
        RequireUtc(nowUtc);
        return new PlatformFactorChallenge
        {
            UserId = userId,
            BindingHash = bindingHash,
            SecurityStamp = securityStamp,
            ReturnUrl = returnUrl,
            Step = step,
            ProtectedSecret = protectedSecret,
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc + Lifetime,
        };
    }

    private static void RequireUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("The instant must be UTC.", nameof(value));
    }
}
