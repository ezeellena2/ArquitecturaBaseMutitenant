namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>La puerta de Registro es la única que puede crear una identidad.</summary>
public sealed record ExternalSignInRequest(string? ReturnUrl, bool IsSignup, bool AcceptedTerms,
    string? Culture = null, string? TimeZoneId = null);
