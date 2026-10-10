using ArquitecturaBaseMultitenant.Application.Models.Profile;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;

public interface IModuleCapabilityContributor
{
    Task<IReadOnlyList<ModuleCapabilityResponse>> ListAsync(CancellationToken cancellationToken);
}
