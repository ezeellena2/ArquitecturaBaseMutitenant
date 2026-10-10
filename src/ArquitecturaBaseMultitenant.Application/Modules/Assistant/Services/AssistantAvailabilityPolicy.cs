using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>Consumer/Platform permanecen apagados en T01; T02 aporta configuración persistente. Business conserva TenantFeatures.</summary>
internal sealed class AssistantAvailabilityPolicy : IFeatureAvailabilityPolicy
{
    public string Key => "assistant";
    public Task<bool?> IsAvailableAsync(Access access, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<bool?>(access == Access.Business ? null : false);
    }
}
