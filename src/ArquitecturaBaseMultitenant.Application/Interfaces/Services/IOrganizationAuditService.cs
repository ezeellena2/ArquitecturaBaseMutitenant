using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IOrganizationAuditService
{
    Task<Result<CursorResult<OrganizationAuditEntryResponse>>> ListAsync(ListOrganizationAuditRequest request,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<OrganizationAuditActorResponse>>> ListActorsAsync(ListOrganizationAuditRequest request,
        CancellationToken cancellationToken);
}
