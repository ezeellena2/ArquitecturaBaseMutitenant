using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Domain.Features;

/// <summary>Describe un módulo del producto; el núcleo siempre existe y no pertenece al catálogo.</summary>
public sealed record FeatureDefinition(string Key, FeatureScope Scope, bool DefaultEnabled)
{
    public bool Supports(TenantKind kind) => (Scope, kind) switch
    {
        (FeatureScope.Consumer or FeatureScope.Both, TenantKind.Personal) => true,
        (FeatureScope.Business or FeatureScope.Both, TenantKind.Business) => true,
        _ => false,
    };
}
