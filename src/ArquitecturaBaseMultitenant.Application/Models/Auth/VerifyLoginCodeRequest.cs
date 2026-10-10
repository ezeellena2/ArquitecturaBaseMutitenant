using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>
/// Transporta el correo, el código recibido y el retorno solicitado para completar el ingreso. El servicio
/// valida la prueba y el destino antes de conceder la sesión.
/// </summary>
public sealed record VerifyLoginCodeRequest(Email? Email, string? Code, string? ReturnUrl, PhoneInputRequest? Phone = null, string? Channel = null, string? Access = null)
{
    public override string ToString() => nameof(VerifyLoginCodeRequest);
}
