using ArquitecturaBaseMultitenant.Api.Json;
using ArquitecturaBaseMultitenant.Api.Contracts.Common;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Auth;

/// <summary>Verifica el código y registra la aceptación vigente en la misma transacción.</summary>
public sealed record VerifySignupHttpRequest(string? Email, [property: RawText] string? Code, bool AcceptedTerms,
    string? Culture = null, string? TimeZoneId = null, PhoneInputHttpRequest? Phone = null, string? Channel = null)
{
    public override string ToString() => nameof(VerifySignupHttpRequest);
}
