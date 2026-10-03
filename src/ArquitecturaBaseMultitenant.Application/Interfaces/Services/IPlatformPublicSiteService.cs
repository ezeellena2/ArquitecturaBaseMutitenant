using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IPlatformPublicSiteService
{
    Task<Result<PlatformPublicSiteResponse>> GetAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<Result> UnpublishAsync(Guid tenantId, PlatformTenantStateRequest request, CancellationToken cancellationToken);
    Task<Result> AllowPublishAsync(Guid tenantId, PlatformTenantStateRequest request, CancellationToken cancellationToken);
}
