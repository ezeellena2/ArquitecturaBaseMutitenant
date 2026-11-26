using ArquitecturaBaseMultitenant.Domain.Features;

namespace ArquitecturaBaseMultitenant.Application.Models.Platform;

/// <summary>Habilitación administrable; cero identifica la configuración inicial sin fila persistida.</summary>
public sealed record PlatformFeatureResponse(string Key, bool Enabled, PlatformFeatureSource Source,
    DateOnly? ExpiresOn, uint Version)
{
    /// <summary>Traduce el origen del dominio. Uno que Plataforma no administra es un bug de quien eligió la fila.</summary>
    internal static PlatformFeatureResponse From(string key, bool enabled, TenantFeatureSource source,
        DateOnly? expiresOn, uint version) => new(key, enabled, source switch
        {
            TenantFeatureSource.Plan => PlatformFeatureSource.Plan,
            TenantFeatureSource.Manual => PlatformFeatureSource.Manual,
            TenantFeatureSource.Trial => PlatformFeatureSource.Trial,
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Plataforma no administra este origen."),
        }, expiresOn, version);
}
