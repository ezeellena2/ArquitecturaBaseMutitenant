using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IPlatformDomainService
{
    Task<Result<IReadOnlyList<OrganizationDomainResponse>>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<Result> VerifyAsync(Guid tenantId, string domain, PlatformTenantStateRequest request, CancellationToken cancellationToken);
    Task<Result> RemoveAsync(Guid tenantId, string domain, PlatformTenantStateRequest request, CancellationToken cancellationToken);
}
