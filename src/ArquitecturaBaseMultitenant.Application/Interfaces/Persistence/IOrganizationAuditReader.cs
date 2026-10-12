using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Auditing;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface IOrganizationAuditReader
{
    Task<CursorResult<OrganizationAuditRow>> ListAsync(ListOrganizationAuditRequest request,
        DateTime? startUtc, DateTime? endUtc, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrganizationAuditActorRow>> ListActorsAsync(ListOrganizationAuditRequest request,
        DateTime? startUtc, DateTime? endUtc, CancellationToken cancellationToken);

    Task<PagedResult<OrganizationAuditActorRow>> ListActorsPagedAsync(ListOrganizationAuditRequest filters,
        ListOrganizationAuditActorsRequest page, DateTime? startUtc, DateTime? endUtc, CancellationToken cancellationToken);
}
