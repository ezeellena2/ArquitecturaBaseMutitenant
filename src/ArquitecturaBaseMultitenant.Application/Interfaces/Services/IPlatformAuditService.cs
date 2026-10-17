using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Auditoría de seguridad de Plataforma: listado por cursor y detalle.</summary>
public interface IPlatformAuditService
{
    Task<Result<CursorResult<PlatformAuditEventResponse>>> ListAsync(ListPlatformAuditRequest request,
        CancellationToken ct);

    Task<Result<PlatformAuditEventResponse>> FindAsync(Guid id, CancellationToken ct);

    Task<Result<IReadOnlyList<PlatformAuditOrganizationResponse>>> ListOrganizationsAsync(
        ListPlatformAuditRequest request, CancellationToken ct);

    Task<Result<PagedResult<PlatformAuditOrganizationResponse>>> ListOrganizationsPagedAsync(
        ListPlatformAuditOrganizationsRequest request, CancellationToken ct);
}
