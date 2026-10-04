using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Platform;

public static class AccountRecoveryErrors
{
    public const string VerificationRequiredCode = "Platform.AccountRecovery.VerificationRequired";

    /// <summary>
    /// El pedido no tiene un código verificado con su nonce, el código venció o ya registró un pedido. Es la misma
    /// respuesta exista o no la cuenta.
    /// </summary>
    public static readonly Error VerificationRequired = Error.Validation(VerificationRequiredCode,
        "The new method has not been verified for this request.");
}
