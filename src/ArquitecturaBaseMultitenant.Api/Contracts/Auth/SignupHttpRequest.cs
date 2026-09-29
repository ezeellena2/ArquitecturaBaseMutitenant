namespace ArquitecturaBaseMultitenant.Api.Contracts.Auth;

/// <summary>Solicitud de alta; cultura y zona proceden del navegador, sin campos visibles nuevos.</summary>
public sealed record SignupHttpRequest(string? Email, bool AcceptedTerms,
    string? Culture = null, string? TimeZoneId = null)
{
    public override string ToString() => nameof(SignupHttpRequest);
}
