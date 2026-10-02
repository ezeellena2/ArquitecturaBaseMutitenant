namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>Datos de la primera organización y empresa que crea una identidad empresarial autenticada.</summary>
public sealed record BusinessSignupRequest(string? OrganizationName, string? Slug, string? FirstCompanyName,
    BusinessSignupTaxId? TaxId, bool AcceptedTerms);

/// <summary>Contrato fiscal del catálogo; el país se deriva de Type y no se recibe dos veces.</summary>
public sealed record BusinessSignupTaxId(string? Type, string? Number);
