using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Organization;

public sealed record CompanyListHttpQuery(int? Page, int? PageSize, string? Sort, string? Search,
    CompanyStatus? Status);

/// <summary>Solo el número de identificación fiscal: el tipo sale del país de la organización.</summary>
public sealed record CreateCompanyHttpRequest(string? Name, string? TaxIdNumber, string? TimeZoneId)
{
    public override string ToString() => nameof(CreateCompanyHttpRequest);
}

/// <summary>Solo el número de identificación fiscal: el tipo sale del país de la organización.</summary>
public sealed record UpdateCompanyHttpRequest(string? Name, string? TaxIdNumber, string? TimeZoneId, uint? Version)
{
    public override string ToString() => nameof(UpdateCompanyHttpRequest);
}

public sealed record ChangeCompanyStatusHttpRequest(CompanyStatus? Status, uint? Version);

public static class CompanyHttpMappings
{
    public static ListCompaniesRequest ToModel(this CompanyListHttpQuery request) => new()
    {
        Page = request.Page ?? PagedRequest.DefaultPage,
        PageSize = request.PageSize ?? PagedRequest.DefaultPageSize,
        Sort = request.Sort,
        Search = request.Search,
        Status = request.Status,
    };
}
