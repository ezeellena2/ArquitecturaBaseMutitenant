using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Domain.Features;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantFeatureDefinitionContributor : IFeatureDefinitionContributor
{
    public IReadOnlyList<FeatureDefinition> Definitions { get; } = [new("assistant", FeatureScope.Both, false)];
}
