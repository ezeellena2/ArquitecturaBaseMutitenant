using ArquitecturaBaseMultitenant.Domain.Assistant;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;

public interface IAssistantSettingsRepository
{
    Task<AssistantPlatformSettings?> GetPlatformAsync(uint expectedVersion, CancellationToken ct);
    Task<AssistantOrganizationSettings?> GetOrganizationAsync(uint expectedVersion, CancellationToken ct);
    Task<AssistantOrganizationPolicy?> GetPolicyAsync(Guid tenantId, uint expectedVersion, CancellationToken ct);
    void Add(AssistantPlatformSettings settings);
    void Add(AssistantOrganizationSettings settings);
    void Add(AssistantOrganizationPolicy policy);
    void MarkUpdated(AssistantPlatformSettings settings);
    void MarkUpdated(AssistantOrganizationSettings settings);
    void MarkUpdated(AssistantOrganizationPolicy policy);
}
