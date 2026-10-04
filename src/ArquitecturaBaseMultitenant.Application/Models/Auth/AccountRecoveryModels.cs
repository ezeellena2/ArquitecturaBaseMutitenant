using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>
/// Pide el código para el método nuevo de «Recuperar mi cuenta». El nonce lo genera el navegador y solo viaja en el
/// cuerpo: ata el código a ese pedido.
/// </summary>
public sealed record RequestRecoveryCodeRequest(Email? Email, string? Nonce)
{
    public override string ToString() => nameof(RequestRecoveryCodeRequest);
}

/// <summary>Verifica el código del método nuevo con el mismo nonce con el que se pidió.</summary>
public sealed record VerifyRecoveryCodeRequest(Email? Email, string? Nonce, string? Code)
{
    public override string ToString() => nameof(VerifyRecoveryCodeRequest);
}

/// <summary>
/// Registra el pedido con el método nuevo ya verificado y el método con el que la persona entraba: un correo o un
/// teléfono, no los dos.
/// </summary>
public sealed record SubmitAccountRecoveryRequest(Email? Email, string? Nonce, Email? PreviousEmail,
    string? PreviousPhoneCountry, string? PreviousPhoneNumber)
{
    public override string ToString() => nameof(SubmitAccountRecoveryRequest);
}
