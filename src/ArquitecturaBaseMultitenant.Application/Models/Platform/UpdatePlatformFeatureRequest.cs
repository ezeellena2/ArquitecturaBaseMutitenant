using ArquitecturaBaseMultitenant.Domain.Features;

namespace ArquitecturaBaseMultitenant.Application.Models.Platform;

/// <summary>Cambio explícito de un módulo global, con concurrencia y motivo.</summary>
public sealed record UpdatePlatformFeatureRequest(bool? Enabled, PlatformFeatureSource? Source,
    DateOnly? ExpiresOn, uint? Version, string? Reason)
{
    /// <summary>El origen del dominio que escribe el writer; el validador ya rechazó un valor fuera del enum.</summary>
    internal TenantFeatureSource? DomainSource => Source switch
    {
        null => null,
        PlatformFeatureSource.Plan => TenantFeatureSource.Plan,
        PlatformFeatureSource.Manual => TenantFeatureSource.Manual,
        PlatformFeatureSource.Trial => TenantFeatureSource.Trial,
        _ => throw new InvalidOperationException("El origen de Plataforma no pasó por el validador."),
    };

    public override string ToString() => nameof(UpdatePlatformFeatureRequest);
}
