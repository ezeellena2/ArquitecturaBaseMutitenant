using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>Pide el código; la aceptación vuelve a validarse cuando se crea la cuenta.</summary>
public sealed record SignupRequest(Email? Email, bool AcceptedTerms,
    string? Culture = null, string? TimeZoneId = null);
