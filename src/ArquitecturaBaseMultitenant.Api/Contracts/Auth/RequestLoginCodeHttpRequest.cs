using ArquitecturaBaseMultitenant.Api.Contracts.Common;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Auth;

/// <summary>Correo al que se solicita un código para una identidad existente.</summary>
public sealed record RequestLoginCodeHttpRequest(string? Email, PhoneInputHttpRequest? Phone = null, string? Channel = null)
{
    public override string ToString() => nameof(RequestLoginCodeHttpRequest);
}
