using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IPlatformSettingsService
{
    Task<Result<PlatformSettingsResponse>> GetAsync(CancellationToken cancellationToken);
    Task<Result> UpdateAsync(UpdatePlatformSettingsRequest request, CancellationToken cancellationToken);
}
