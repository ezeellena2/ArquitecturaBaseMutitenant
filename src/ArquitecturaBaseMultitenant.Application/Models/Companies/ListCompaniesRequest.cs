using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Models.Companies;

public sealed record ListCompaniesRequest : PagedRequest
{
    public static readonly IReadOnlyCollection<string> SortableFields = ["name", "taxId", "timeZoneId", "memberCount", "status", "createdAtUtc"];
    public CompanyStatus? Status { get; init; }
}
