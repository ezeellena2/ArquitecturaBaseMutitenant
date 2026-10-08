using ArquitecturaBaseMultitenant.Api.Contracts.Common;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Account;

/// <summary>
/// Recibe el correo que se quiere agregar a la cuenta y lo entrega al flujo de validación. ToString oculta
/// los datos sensibles para que no se impriman al registrar la solicitud.
/// </summary>
public sealed record AddLoginMethodHttpRequest(string? Email, PhoneInputHttpRequest? Phone = null, string? Channel = null)
{
    public override string ToString() => nameof(AddLoginMethodHttpRequest);
}
