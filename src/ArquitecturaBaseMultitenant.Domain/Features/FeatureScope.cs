namespace ArquitecturaBaseMultitenant.Domain.Features;

/// <summary>Accesos en los que existe un módulo opcional.</summary>
public enum FeatureScope
{
    Consumer,
    Business,
    Both,
}
