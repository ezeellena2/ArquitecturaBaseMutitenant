namespace ArquitecturaBaseMultitenant.Domain.Features;

/// <summary>Catálogo de módulos reales del producto. La plantilla no agrega módulos de ejemplo.</summary>
public static class Features
{
    public static IReadOnlyList<FeatureDefinition> All { get; } = Array.AsReadOnly<FeatureDefinition>([]);
}
