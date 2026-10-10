using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Profile;

namespace ArquitecturaBaseMultitenant.Application.Services.Profile;

internal sealed class ProfileModulesBuilder(IFeatureService features, IEnumerable<IModuleCapabilityContributor> contributors)
{
    internal Task<IReadOnlyList<string>> ListEnabledAsync(CancellationToken cancellationToken) => features.ListEnabledAsync(cancellationToken);
    internal async Task<IReadOnlyList<ModuleCapabilityResponse>> CapabilitiesAsync(CancellationToken cancellationToken)
    {
        var capabilities = new List<ModuleCapabilityResponse>();
        foreach (var contributor in contributors) capabilities.AddRange(await contributor.ListAsync(cancellationToken));
        if (capabilities.Select(value => value.Key).Distinct(StringComparer.Ordinal).Count() != capabilities.Count)
            throw new InvalidOperationException("Module capability keys must be unique.");
        return capabilities;
    }
}
