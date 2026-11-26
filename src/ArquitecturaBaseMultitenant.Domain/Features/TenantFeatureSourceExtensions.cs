namespace ArquitecturaBaseMultitenant.Domain.Features;

public static class TenantFeatureSourceExtensions
{
    /// <summary>La fila que lee y edita la pestaña «Módulos» de Plataforma: Plan, Manual o Trial (ADR 0069).</summary>
    public static bool IsPlatformManaged(this TenantFeatureSource source) =>
        source is TenantFeatureSource.Plan or TenantFeatureSource.Manual or TenantFeatureSource.Trial;
}
