using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>
/// La configuración de WhatsApp para guardarla (B18): con su lock y la versión que leyó la pantalla (0 sin fila); otra versión
/// es el 409 de siempre. La de la organización es la del token.
/// </summary>
public interface IWhatsAppSettingsRepository
{
    Task<WhatsAppPlatformSettings?> GetPlatformForUpdateAsync(uint expectedVersion, CancellationToken ct);
    Task<WhatsAppOrganizationSettings?> GetOrganizationForUpdateAsync(uint expectedVersion, CancellationToken ct);
    void Add(WhatsAppPlatformSettings settings);
    void Add(WhatsAppOrganizationSettings settings);
    void MarkUpdated(WhatsAppPlatformSettings settings);
    void MarkUpdated(WhatsAppOrganizationSettings settings);
}

/// <summary>La configuración de WhatsApp para leerla en cada uso (270); null sin fila.</summary>
public interface IWhatsAppSettingsReader
{
    Task<WhatsAppPlatformSettingsRow?> FindPlatformAsync(CancellationToken ct);

    /// <summary>La de la organización activa (RLS); null sin fila.</summary>
    Task<WhatsAppOrganizationSettingsRow?> FindOrganizationAsync(CancellationToken ct);
}

public sealed record WhatsAppPlatformSettingsRow(IReadOnlyList<string> AllowedCountries, int ChatRetentionDays,
    int MaxOrganizationChatRetentionDays, uint Version);

public sealed record WhatsAppOrganizationSettingsRow(int ChatRetentionDays, uint Version);
