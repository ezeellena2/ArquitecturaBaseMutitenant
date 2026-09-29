namespace ArquitecturaBaseMultitenant.Api.Contracts.Auth;

/// <summary>Verifica el código y registra la aceptación vigente en la misma transacción.</summary>
public sealed record VerifySignupHttpRequest(string? Email, string? Code, bool AcceptedTerms,
    string? Culture = null, string? TimeZoneId = null)
{
    public override string ToString() => nameof(VerifySignupHttpRequest);
}
