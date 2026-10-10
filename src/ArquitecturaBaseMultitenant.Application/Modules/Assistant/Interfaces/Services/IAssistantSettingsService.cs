using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;

public interface IAssistantSettingsService
{
    Task<Result<OrganizationAssistantSettingsResponse>> GetOrganizationAsync(CancellationToken cancellationToken);
    Task<Result<VersionResponse>> UpdateOrganizationAsync(UpdateOrganizationAssistantSettingsRequest request, CancellationToken cancellationToken);
    Task<Result<PlatformAssistantSettingsResponse>> GetPlatformAsync(CancellationToken cancellationToken);
    Task<Result<VersionResponse>> UpdatePlatformAsync(UpdatePlatformAssistantSettingsRequest request, CancellationToken cancellationToken);
}
