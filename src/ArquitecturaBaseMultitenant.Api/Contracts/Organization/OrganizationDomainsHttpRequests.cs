using ArquitecturaBaseMultitenant.Application.Models.Tenancy;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Organization;

public sealed record RegisterOrganizationDomainHttpRequest(string? Domain)
{
    public override string ToString() => nameof(RegisterOrganizationDomainHttpRequest);
}

public static class OrganizationDomainsHttpMappings
{
    public static RegisterOrganizationDomainRequest ToModel(this RegisterOrganizationDomainHttpRequest request) =>
        new(request.Domain);
}
