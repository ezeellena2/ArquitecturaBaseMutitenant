namespace ArquitecturaBaseMultitenant.Api.Contracts.Auth;

/// <summary>Verifica el código y vuelve a la autorización local.</summary>
public sealed record VerifyLoginCodeHttpRequest(string? Email, string? Code, string? ReturnUrl)
{
    public override string ToString() => nameof(VerifyLoginCodeHttpRequest);
}
