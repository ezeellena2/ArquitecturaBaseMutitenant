using ArquitecturaBaseMultitenant.Api.Json;
using ArquitecturaBaseMultitenant.Api.Contracts.Common;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Auth;

/// <summary>Correo con el que se entraba, enviado sólo en el cuerpo.</summary>
public sealed record PreviewAccountRecoveryHttpRequest(string? PreviousEmail, PhoneInputHttpRequest? PreviousPhone = null)
{
    public override string ToString() => nameof(PreviewAccountRecoveryHttpRequest);
}

/// <summary>Correo nuevo y nonce del navegador; el nonce no vuelve en ninguna respuesta.</summary>
public sealed record RequestRecoveryCodeHttpRequest(string? Email, [property: RawText] string? Nonce, PhoneInputHttpRequest? Phone = null, string? Channel = null)
{
    public override string ToString() => nameof(RequestRecoveryCodeHttpRequest);
}

/// <summary>Código recibido en el correo nuevo, con el mismo nonce del pedido del código.</summary>
public sealed record VerifyRecoveryCodeHttpRequest(string? Email, [property: RawText] string? Nonce, [property: RawText] string? Code, PhoneInputHttpRequest? Phone = null, string? Channel = null)
{
    public override string ToString() => nameof(VerifyRecoveryCodeHttpRequest);
}

/// <summary>El correo nuevo ya verificado y el método con el que se entraba: un correo o un teléfono.</summary>
public sealed record SubmitAccountRecoveryHttpRequest(string? Email, [property: RawText] string? Nonce, string? PreviousEmail,
    PhoneInputHttpRequest? PreviousPhone = null, PhoneInputHttpRequest? Phone = null, string? Channel = null)
{
    public override string ToString() => nameof(SubmitAccountRecoveryHttpRequest);
}
