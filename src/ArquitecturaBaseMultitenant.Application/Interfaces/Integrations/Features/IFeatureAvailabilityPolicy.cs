using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;

public interface IFeatureAvailabilityPolicy
{
    string Key { get; }
    /// <summary>True/false decide este acceso; null delega al catálogo y TenantFeatures. El apagado de emergencia siempre prevalece.</summary>
    Task<bool?> IsAvailableAsync(Access access, CancellationToken cancellationToken);
}
