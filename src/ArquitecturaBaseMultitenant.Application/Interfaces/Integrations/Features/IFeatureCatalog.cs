using ArquitecturaBaseMultitenant.Domain.Features;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;

/// <summary>Catálogo cerrado de módulos reales; el núcleo no aparece en él.</summary>
public interface IFeatureCatalog
{
    IReadOnlyList<FeatureDefinition> All { get; }
}
