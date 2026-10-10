using ArquitecturaBaseMultitenant.Api.Json;
using ArquitecturaBaseMultitenant.Api.Contracts.Common;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Auth;

/// <summary>Verifica el código y continúa un retorno autorizado, de invitación o fijo del mismo acceso.</summary>
public sealed record VerifyLoginCodeHttpRequest(string? Email, [property: RawText] string? Code,
    [property: RawText] string? ReturnUrl, PhoneInputHttpRequest? Phone = null, string? Channel = null,
    [property: RawText] string? Access = null)
{
    public override string ToString() => nameof(VerifyLoginCodeHttpRequest);
}
