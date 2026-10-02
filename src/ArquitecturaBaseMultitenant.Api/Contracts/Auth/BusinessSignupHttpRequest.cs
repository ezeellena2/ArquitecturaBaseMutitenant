namespace ArquitecturaBaseMultitenant.Api.Contracts.Auth;

/// <summary>Recibe los datos de la primera organización y empresa sin imprimir datos fiscales en logs.</summary>
public sealed record BusinessSignupHttpRequest(string? OrganizationName, string? Slug,
    string? FirstCompanyName, string? TaxId, bool AcceptedTerms)
{
    public override string ToString() => nameof(BusinessSignupHttpRequest);
}
