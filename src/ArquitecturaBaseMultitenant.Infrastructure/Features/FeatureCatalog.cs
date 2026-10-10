using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Domain.Features;

namespace ArquitecturaBaseMultitenant.Infrastructure.Features;

internal sealed class FeatureCatalog : IFeatureCatalog
{
    public FeatureCatalog(IEnumerable<IFeatureDefinitionContributor> contributors)
    {
        All = [.. Domain.Features.Features.All, .. contributors.SelectMany(contributor => contributor.Definitions)];
        if (All.Select(definition => definition.Key).Distinct(StringComparer.Ordinal).Count() != All.Count)
            throw new InvalidOperationException("Feature keys must be unique across all contributors.");
    }
    public IReadOnlyList<FeatureDefinition> All { get; }
}
