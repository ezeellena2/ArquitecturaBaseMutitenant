using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Models.Settings;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IOrganizationSettingsService
{
    Task<Result<OrganizationSettingsResponse>> GetAsync(CancellationToken cancellationToken);

    Task<Result<VersionResponse>> UpdateAsync(UpdateOrganizationSettingsRequest request, CancellationToken cancellationToken);
}
