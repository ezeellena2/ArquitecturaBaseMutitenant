using ArquitecturaBaseMultitenant.Application.Common.Pagination;

namespace ArquitecturaBaseMultitenant.Application.Models.Tenancy;

public sealed record ListOrganizationDomainsRequest : PagedRequest
{
    public static readonly IReadOnlyCollection<string> SortableFields = ["domain"];
}
