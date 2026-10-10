using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Domain.Features;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures;

// Supplies the module named by the help catalog without installing a provider or changing production defaults.
internal sealed class HelpModuleFeature : IFeatureDefinitionContributor
{
    public IReadOnlyList<FeatureDefinition> Definitions { get; } = [new("whatsapp", FeatureScope.Consumer, true)];
}
