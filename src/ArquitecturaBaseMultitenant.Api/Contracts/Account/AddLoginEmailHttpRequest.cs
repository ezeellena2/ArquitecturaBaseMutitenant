using ArquitecturaBaseMultitenant.Api.Json;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Account;

/// <summary>
/// Recibe el correo que se quiere agregar a la cuenta y lo entrega al flujo de validación. ToString oculta
/// los datos sensibles para que no se impriman al registrar la solicitud.
/// </summary>
public sealed record AddLoginEmailHttpRequest(string? Email, [property: RawText] string? ReauthTicket)
{
    public override string ToString() => nameof(AddLoginEmailHttpRequest);
}
