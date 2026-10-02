namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>Datos de la primera organización y empresa que crea una identidad empresarial autenticada.</summary>
public sealed record BusinessSignupRequest(string? OrganizationName, string? Slug, string? FirstCompanyName,
    string? TaxId, bool AcceptedTerms);
