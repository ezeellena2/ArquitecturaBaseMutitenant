using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Administra habilitaciones globales de organizaciones, sin acceder a filas privadas.</summary>
public interface IPlatformFeatureService
{
    Task<Result<IReadOnlyList<PlatformFeatureResponse>>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<Result> UpdateAsync(Guid tenantId, string key, UpdatePlatformFeatureRequest request, CancellationToken cancellationToken);
}
