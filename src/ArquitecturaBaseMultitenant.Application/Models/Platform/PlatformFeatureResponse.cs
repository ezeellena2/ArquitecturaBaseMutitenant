using ArquitecturaBaseMultitenant.Domain.Features;

namespace ArquitecturaBaseMultitenant.Application.Models.Platform;

/// <summary>Habilitación administrable; cero identifica la configuración inicial sin fila persistida.</summary>
public sealed record PlatformFeatureResponse(string Key, bool Enabled, TenantFeatureSource Source,
    DateTime? ExpiresAtUtc, uint Version);
