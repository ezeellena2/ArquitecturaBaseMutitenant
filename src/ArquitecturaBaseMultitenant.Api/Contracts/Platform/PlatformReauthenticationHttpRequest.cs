using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Platform;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Platform;

/// <summary>«Confirmá que sos vos» (E9): con qué prueba y el código; nunca se registra.</summary>
public sealed record PlatformReauthenticationHttpRequest(TwoFactorMethod? Method, string? Code)
{
    public PlatformReauthenticationRequest ToModel() => new(Method, Code);
    public override string ToString() => nameof(PlatformReauthenticationHttpRequest);
}
