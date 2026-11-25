using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Lo que vale de la configuración de WhatsApp en cada uso (270), leído de la base: sin fila de la plataforma, los países de
/// <c>WhatsApp:AllowedCountries</c> hasta el despliegue siguiente (0060) y los plazos por defecto; el de una organización, el
/// suyo o el de defecto, nunca más que el máximo que fija la plataforma.
/// </summary>
internal sealed class WhatsAppSettingsResolver(IWhatsAppSettingsReader reader, IWhatsAppChannelSettings channel)
{
    /// <summary>Los países de la configuración de arranque: el respaldo mientras Plataforma no guarda los suyos.</summary>
    public IReadOnlyList<string> ConfiguredCountries => channel.ConfiguredCountries;

    public async Task<WhatsAppPlatformSettingsRow> PlatformAsync(CancellationToken ct) =>
        await reader.FindPlatformAsync(ct) ?? new WhatsAppPlatformSettingsRow([.. ConfiguredCountries],
            WhatsAppSettingsLimits.DefaultChatRetentionDays, WhatsAppSettingsLimits.DefaultMaxOrganizationChatRetentionDays, 0);

    public async Task<IReadOnlyList<string>> AllowedCountriesAsync(CancellationToken ct) => (await PlatformAsync(ct)).AllowedCountries;

    /// <summary>El plazo de los chats de la organización activa, recortado al máximo de la plataforma.</summary>
    public async Task<int> OrganizationChatRetentionDaysAsync(CancellationToken ct)
    {
        var platform = await PlatformAsync(ct);
        var own = (await reader.FindOrganizationAsync(ct))?.ChatRetentionDays ?? WhatsAppSettingsLimits.DefaultChatRetentionDays;
        return Math.Min(own, platform.MaxOrganizationChatRetentionDays);
    }
}
