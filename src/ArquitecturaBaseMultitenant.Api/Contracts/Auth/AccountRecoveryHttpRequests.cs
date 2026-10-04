using ArquitecturaBaseMultitenant.Api.Contracts.Common;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Auth;

/// <summary>Correo nuevo y nonce del navegador; el nonce no vuelve en ninguna respuesta.</summary>
public sealed record RequestRecoveryCodeHttpRequest(string? Email, string? Nonce)
{
    public override string ToString() => nameof(RequestRecoveryCodeHttpRequest);
}

/// <summary>Código recibido en el correo nuevo, con el mismo nonce del pedido del código.</summary>
public sealed record VerifyRecoveryCodeHttpRequest(string? Email, string? Nonce, string? Code)
{
    public override string ToString() => nameof(VerifyRecoveryCodeHttpRequest);
}

/// <summary>El correo nuevo ya verificado y el método con el que se entraba: un correo o un teléfono.</summary>
public sealed record SubmitAccountRecoveryHttpRequest(string? Email, string? Nonce, string? PreviousEmail,
    PhoneInputHttpRequest? PreviousPhone)
{
    public override string ToString() => nameof(SubmitAccountRecoveryHttpRequest);
}
