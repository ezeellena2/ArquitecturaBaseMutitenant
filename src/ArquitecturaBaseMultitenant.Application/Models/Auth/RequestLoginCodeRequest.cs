using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>
/// Transporta el correo al que se solicita un código de ingreso. La respuesta del flujo evita revelar si
/// existe una cuenta con ese correo.
/// </summary>
public sealed record RequestLoginCodeRequest(Email? Email, PhoneInputRequest? Phone = null, string? Channel = null)
{
    public override string ToString() => nameof(RequestLoginCodeRequest);
}
