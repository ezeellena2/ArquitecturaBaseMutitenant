using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>
/// Si la conexión de quien paga puede andar acá, sin llamar al proveedor (ADR 0064 §6 y §7, B13): la clave guardada se puede
/// descifrar, y el proveedor guardado es uno que esta versión conoce. Lo usan la configuración del turno antes de reservar, el
/// estado del asistente y las dos configuraciones. Una petición prueba la misma clave una sola vez.
/// </summary>
internal sealed class AssistantConnectionPolicy(IAssistantSettingsReader settings, IAssistantModelReader models, IAssistantKeyProtector protector)
{
    private readonly Dictionary<AssistantMode, bool> unreadable = [];

    /// <summary>
    /// La clave guardada de esa financiación no se puede descifrar con el anillo de esta copia. Sin clave guardada (o con la de
    /// la configuración de arranque) no hay nada que probar.
    /// </summary>
    internal async Task<bool> IsKeyUnreadableAsync(AssistantMode funding, CancellationToken ct)
    {
        if (unreadable.TryGetValue(funding, out var known)) return known;
        var material = funding == AssistantMode.OwnKey ? await settings.GetOrganizationKeyAsync(ct) : await settings.GetPlatformKeyAsync(ct);
        var result = material.Ciphertext is { } cipher && !(funding == AssistantMode.OwnKey
            ? protector.CanUnprotectOrganization(cipher) : protector.CanUnprotectPlatform(cipher));
        unreadable[funding] = result;
        return result;
    }

    /// <summary>El modelo guardado es de un proveedor que esta versión no conoce: la conexión queda pausada.</summary>
    internal async Task<bool> IsUnsupportedAsync(Guid modelId, CancellationToken ct) =>
        await models.FindProviderAsync(modelId, ct) is { } provider && !IsKnown(provider);

    internal static bool IsKnown(string provider) =>
        Enum.TryParse<AssistantProvider>(provider, false, out var parsed) && Enum.IsDefined(parsed) && parsed.ToString() == provider;
}
