using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
public interface IAssistantSettingsReader
{
    Task<bool> IsPersonalEnabledAsync(CancellationToken ct);
    Task<PlatformAssistantSettingsResponse> FindPlatformAsync(CancellationToken ct);
    Task<AssistantOrganizationSettingsRow?> FindOrganizationAsync(CancellationToken ct);
    Task<AssistantPolicyResponse> FindPolicyAsync(Guid tenantId, CancellationToken ct);
    Task<AssistantKeyMaterial> GetPlatformKeyAsync(CancellationToken ct);
    Task<AssistantKeyMaterial> GetOrganizationKeyAsync(CancellationToken ct);
}
