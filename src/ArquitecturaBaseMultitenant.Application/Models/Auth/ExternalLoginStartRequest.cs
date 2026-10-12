namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>Entrada del desafío Google; SignupEndpoint lo define el adaptador HTTP, no el visitante.</summary>
public sealed record ExternalLoginStartRequest(string? ReturnUrl, bool Signup, bool AcceptedTerms,
    string? ReturnTo, bool SignupEndpoint)
{
    public override string ToString() => nameof(ExternalLoginStartRequest);
}
