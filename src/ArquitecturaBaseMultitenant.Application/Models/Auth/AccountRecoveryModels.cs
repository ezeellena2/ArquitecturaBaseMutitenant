using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>Consulta el correo anterior sin emitir un código ni conceder una sesión.</summary>
public sealed record PreviewAccountRecoveryRequest(Email? PreviousEmail, PhoneInputRequest? PreviousPhone = null)
{
    public override string ToString() => nameof(PreviewAccountRecoveryRequest);
}

/// <summary>
/// Pide el código para el método nuevo de «Recuperar mi cuenta». El nonce lo genera el navegador y solo viaja en el
/// cuerpo: ata el código a ese pedido.
/// </summary>
public sealed record RequestRecoveryCodeRequest(Email? Email, string? Nonce, PhoneInputRequest? Phone = null, string? Channel = null)
{
    public override string ToString() => nameof(RequestRecoveryCodeRequest);
}

/// <summary>Verifica el código del método nuevo con el mismo nonce con el que se pidió.</summary>
public sealed record VerifyRecoveryCodeRequest(Email? Email, string? Nonce, string? Code, PhoneInputRequest? Phone = null, string? Channel = null)
{
    public override string ToString() => nameof(VerifyRecoveryCodeRequest);
}

/// <summary>
/// Registra el pedido con el método nuevo ya verificado y el método con el que la persona entraba: un correo o un
/// teléfono, no los dos.
/// </summary>
public sealed record SubmitAccountRecoveryRequest(Email? Email, string? Nonce, Email? PreviousEmail,
    string? PreviousPhoneCountry, string? PreviousPhoneNumber, PhoneInputRequest? Phone = null, string? Channel = null)
{
    public override string ToString() => nameof(SubmitAccountRecoveryRequest);
}
