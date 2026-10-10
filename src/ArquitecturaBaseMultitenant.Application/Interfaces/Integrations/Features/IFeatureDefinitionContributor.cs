using ArquitecturaBaseMultitenant.Domain.Features;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;

public interface IFeatureDefinitionContributor
{
    IReadOnlyList<FeatureDefinition> Definitions { get; }
}
