using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
public interface IAssistantSettingsReader
{
    Task<bool> IsPersonalEnabledAsync(CancellationToken ct);
    Task<PlatformAssistantSettingsResponse> FindPlatformAsync(CancellationToken ct);
}
