using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IPlatformPublicSiteService
{
    Task<Result<PlatformPublicSiteResponse>> GetAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<Result<VersionResponse>> UnpublishAsync(Guid tenantId, PlatformTenantStateRequest request, CancellationToken cancellationToken);
    Task<Result<VersionResponse>> AllowPublishAsync(Guid tenantId, PlatformTenantStateRequest request, CancellationToken cancellationToken);
}
