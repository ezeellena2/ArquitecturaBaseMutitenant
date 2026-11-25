using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;

public interface IAssistantSettingsService
{
    Task<Result<OrganizationAssistantSettingsResponse>> GetOrganizationAsync(CancellationToken cancellationToken);
    Task<Result<VersionResponse>> UpdateOrganizationAsync(UpdateOrganizationAssistantSettingsRequest request, CancellationToken cancellationToken);
    Task<Result<PlatformAssistantSettingsResponse>> GetPlatformAsync(CancellationToken cancellationToken);
    Task<Result<VersionResponse>> UpdatePlatformAsync(UpdatePlatformAssistantSettingsRequest request, CancellationToken cancellationToken);
    /// <summary>
    /// La versión de la configuración de quien paga (B13). El worker la lee antes de armar el cliente del turno y la devuelve con
    /// lo que dijo el proveedor: así una respuesta a la clave o el modelo que alguien ya reemplazó no se anota sobre los nuevos.
    /// </summary>
    Task<Result<uint>> GetSettingsVersionAsync(AssistantMode funding, CancellationToken cancellationToken);
    /// <summary>
    /// Lo que dijo el proveedor en un turno (ADR 0064 §7): guarda para quien paga la falla que arregla quien la configura, o la
    /// limpia con <c>null</c> cuando respondió. Otro código no cambia nada, y tampoco una configuración guardada después de
    /// <paramref name="settingsVersion"/>. No cambia la versión de la configuración. Lo llama el worker, fuera del turno.
    /// </summary>
    Task<Result> RecordProviderOutcomeAsync(AssistantMode funding, string? failureCode, uint settingsVersion, CancellationToken cancellationToken);
}
