using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Organization;

public sealed record OrganizationDomainListHttpQuery(int? Page, int? PageSize, string? Sort, string? Search)
{
    public ListOrganizationDomainsRequest ToModel() => new()
    {
        Page = Page ?? PagedRequest.DefaultPage, PageSize = PageSize ?? PagedRequest.DefaultPageSize,
        Sort = Sort, Search = Search,
    };
}

public sealed record RegisterOrganizationDomainHttpRequest(string? Domain)
{
    public override string ToString() => nameof(RegisterOrganizationDomainHttpRequest);
}

public static class OrganizationDomainsHttpMappings
{
    public static RegisterOrganizationDomainRequest ToModel(this RegisterOrganizationDomainHttpRequest request) =>
        new(request.Domain);
}
