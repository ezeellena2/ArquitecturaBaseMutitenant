namespace ArquitecturaBaseMultitenant.Api.Contracts.Auth;

/// <summary>Correo al que se solicita un código para una identidad existente.</summary>
public sealed record RequestLoginCodeHttpRequest(string? Email)
{
    public override string ToString() => nameof(RequestLoginCodeHttpRequest);
}
