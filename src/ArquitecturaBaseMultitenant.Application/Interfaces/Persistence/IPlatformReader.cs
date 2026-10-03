using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Platform;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Proyecciones globales autorizadas; nunca lee tablas tenant ni ignora sus filtros.</summary>
public interface IPlatformReader
{
    Task<PagedResult<PlatformTenantResponse>> ListTenantsAsync(ListPlatformTenantsRequest request, CancellationToken ct);
    Task<PlatformTenantFilterCounts> CountTenantsAsync(ListPlatformTenantsRequest request, CancellationToken ct);
    Task<PlatformTenantResponse?> FindTenantAsync(Guid tenantId, CancellationToken ct);
    Task<IReadOnlyList<Guid>> ListTenantNoticeRecipientsAsync(Guid tenantId, CancellationToken ct);
}
