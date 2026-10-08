using ArquitecturaBaseMultitenant.Api.Json;
using ArquitecturaBaseMultitenant.Api.Contracts.Common;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Auth;

/// <summary>Verifica el código y vuelve a la autorización local.</summary>
public sealed record VerifyLoginCodeHttpRequest(string? Email, [property: RawText] string? Code, string? ReturnUrl, PhoneInputHttpRequest? Phone = null, string? Channel = null)
{
    public override string ToString() => nameof(VerifyLoginCodeHttpRequest);
}
