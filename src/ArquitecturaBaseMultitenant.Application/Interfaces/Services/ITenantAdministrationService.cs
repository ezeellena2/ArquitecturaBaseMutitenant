using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface ITenantAdministrationService
{
    Task<Result<PagedResult<PlatformTenantResponse>>> ListAsync(ListPlatformTenantsRequest request, CancellationToken ct);
    Task<Result<PlatformTenantFilterCounts>> CountAsync(ListPlatformTenantsRequest request, CancellationToken ct);
    Task<Result<PlatformTenantResponse>> FindAsync(Guid tenantId, CancellationToken ct);
    Task<Result<VersionResponse>> ChangeStateAsync(Guid tenantId, PlatformTenantAction action, PlatformTenantStateRequest request, CancellationToken ct);
    Task<Result<CreatedPlatformTenantResponse>> CreateAsync(Guid operationId, CreatePlatformTenantRequest request, CancellationToken ct);
    Task<Result<VersionResponse>> ProvisionAsync(Guid tenantId, bool retry, PlatformTenantStateRequest request, CancellationToken ct);
}
