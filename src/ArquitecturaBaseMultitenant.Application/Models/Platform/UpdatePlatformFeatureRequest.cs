using ArquitecturaBaseMultitenant.Domain.Features;

namespace ArquitecturaBaseMultitenant.Application.Models.Platform;

/// <summary>Cambio explícito de un módulo global, con concurrencia y motivo.</summary>
public sealed record UpdatePlatformFeatureRequest(bool? Enabled, TenantFeatureSource? Source,
    DateOnly? ExpiresOn, uint? Version, string? Reason)
{
    public override string ToString() => nameof(UpdatePlatformFeatureRequest);
}
