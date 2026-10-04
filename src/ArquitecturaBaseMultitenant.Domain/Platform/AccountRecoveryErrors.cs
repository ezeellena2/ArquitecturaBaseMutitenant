using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Platform;

public static class AccountRecoveryErrors
{
    public const string VerificationRequiredCode = "Platform.AccountRecovery.VerificationRequired";
    public const string NotFoundCode = "Platform.AccountRecovery.NotFound";
    public const string AlreadyResolvedCode = "Platform.AccountRecovery.AlreadyResolved";
    public const string NoAccountCode = "Platform.AccountRecovery.NoAccount";
    public const string MethodInUseCode = "Platform.AccountRecovery.MethodInUse";

    /// <summary>
    /// El pedido no tiene un código verificado con su nonce, el código venció o ya registró un pedido. Es la misma
    /// respuesta exista o no la cuenta.
    /// </summary>
    public static readonly Error VerificationRequired = Error.Validation(VerificationRequiredCode,
        "The new method has not been verified for this request.");

    public static readonly Error NotFound = Error.NotFound(NotFoundCode, "The recovery request does not exist.");

    public static readonly Error AlreadyResolved = Error.Conflict(AlreadyResolvedCode,
        "The recovery request has already been resolved.");

    /// <summary>Ninguna cuenta tiene el método de antes: el pedido solo se puede rechazar.</summary>
    public static readonly Error NoAccount = Error.Conflict(NoAccountCode,
        "No account has the previous method; the request can only be rejected.");

    /// <summary>El correo nuevo ya es de una cuenta: el operador lo ve en Cuentas, así que se le puede decir.</summary>
    public static readonly Error MethodInUse = Error.Conflict(MethodInUseCode,
        "The new method already belongs to an account.");
}
