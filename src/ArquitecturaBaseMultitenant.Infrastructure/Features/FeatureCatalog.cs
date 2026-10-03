using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Domain.Features;

namespace ArquitecturaBaseMultitenant.Infrastructure.Features;

/// <summary>Expone el catálogo canónico del producto.</summary>
internal sealed class FeatureCatalog : IFeatureCatalog
{
    public IReadOnlyList<FeatureDefinition> All => Domain.Features.Features.All;
}
