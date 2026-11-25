using ArquitecturaBaseMultitenant.Domain.Assistant;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;

public interface IAssistantSettingsRepository
{
    Task<AssistantPlatformSettings?> GetPlatformAsync(uint expectedVersion, CancellationToken ct);
    Task<AssistantOrganizationSettings?> GetOrganizationAsync(uint expectedVersion, CancellationToken ct);
    Task<AssistantOrganizationPolicy?> GetPolicyAsync(Guid tenantId, uint expectedVersion, CancellationToken ct);
    /// <summary>
    /// La versión de la configuración, con su lock y sin cargarla para escribir (0, sin fila): la falla del proveedor se anota solo
    /// si nadie la guardó desde que el turno armó su cliente (B13).
    /// </summary>
    Task<uint> LockPlatformVersionAsync(CancellationToken ct);
    Task<uint> LockOrganizationVersionAsync(CancellationToken ct);
    /// <summary>La falla anotada, seguida para cambiarla bajo el lock de su configuración (B13).</summary>
    Task<AssistantPlatformProviderIssue?> FindPlatformIssueAsync(CancellationToken ct);
    Task<AssistantOrganizationProviderIssue?> FindOrganizationIssueAsync(CancellationToken ct);
    void Add(AssistantPlatformProviderIssue issue);
    void Add(AssistantOrganizationProviderIssue issue);
    void Add(AssistantPlatformSettings settings);
    void Add(AssistantOrganizationSettings settings);
    void Add(AssistantOrganizationPolicy policy);
    void MarkUpdated(AssistantPlatformSettings settings);
    void MarkUpdated(AssistantOrganizationSettings settings);
    void MarkUpdated(AssistantOrganizationPolicy policy);
}
