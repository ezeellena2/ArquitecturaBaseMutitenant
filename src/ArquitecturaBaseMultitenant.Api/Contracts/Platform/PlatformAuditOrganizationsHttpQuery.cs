using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Platform;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Platform;

public sealed record PlatformAuditOrganizationsHttpQuery(int? Page, int? PageSize, string? Sort, string? Search,
    string? Period = "30", string? Type = null, Guid? AccountId = null, string? AuditSearch = null)
{
    public ListPlatformAuditOrganizationsRequest ToModel() => new()
    {
        Page = Page ?? PagedRequest.DefaultPage, PageSize = PageSize ?? PagedRequest.DefaultPageSize,
        Sort = Sort, Search = Search, Period = Period, Type = Type, AccountId = AccountId, AuditSearch = AuditSearch,
    };
    public override string ToString() => nameof(PlatformAuditOrganizationsHttpQuery);
}
