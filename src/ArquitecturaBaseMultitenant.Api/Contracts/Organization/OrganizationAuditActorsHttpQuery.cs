using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Auditing;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Organization;

public sealed record OrganizationAuditActorsHttpQuery(int? Page, int? PageSize, string? Sort, string? Search,
    string? Period = "30", string? Category = null, string? AuditSearch = null)
{
    public ListOrganizationAuditActorsRequest ToModel() => new()
    {
        Page = Page ?? PagedRequest.DefaultPage, PageSize = PageSize ?? PagedRequest.DefaultPageSize,
        Sort = Sort, Search = Search, Period = Period, Category = Category, AuditSearch = AuditSearch,
    };
    public override string ToString() => nameof(OrganizationAuditActorsHttpQuery);
}
