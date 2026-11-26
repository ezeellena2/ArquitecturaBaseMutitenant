using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Features;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Features;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Lo que vale de la configuración de WhatsApp en cada uso (270), leído de la base: sin fila de la plataforma, los países de
/// <c>WhatsApp:AllowedCountries</c> hasta el despliegue siguiente (0060) y los plazos por defecto; el de una organización, el
/// suyo o el de defecto, nunca más que su tope, que da <see cref="ModuleLimitResolver"/> (ADR 0069 §4).
/// </summary>
internal sealed class WhatsAppSettingsResolver(IWhatsAppSettingsReader reader, IWhatsAppChannelSettings channel,
    ModuleLimitResolver limits, ITenantContext tenant)
{
    /// <summary>Los países de la configuración de arranque: el respaldo mientras Plataforma no guarda los suyos.</summary>
    public IReadOnlyList<string> ConfiguredCountries => channel.ConfiguredCountries;

    public async Task<WhatsAppPlatformSettingsRow> PlatformAsync(CancellationToken ct) =>
        await reader.FindPlatformAsync(ct) ?? new WhatsAppPlatformSettingsRow([.. ConfiguredCountries],
            WhatsAppSettingsLimits.DefaultChatRetentionDays, WhatsAppSettingsLimits.DefaultMaxOrganizationChatRetentionDays, 0);

    public async Task<IReadOnlyList<string>> AllowedCountriesAsync(CancellationToken ct) => (await PlatformAsync(ct)).AllowedCountries;

    /// <summary>El plazo de los chats de la organización activa, recortado a su tope.</summary>
    public async Task<int> OrganizationChatRetentionDaysAsync(CancellationToken ct) => (await OrganizationChatRetentionAsync(ct)).Days;

    /// <summary>
    /// El plazo de la organización activa y el tope que lo recorta: el menor entre el suyo (o el de entrada) y el tope, sin tocar
    /// lo guardado. Si Plataforma baja el máximo, vale el menor desde el uso siguiente.
    /// </summary>
    public async Task<(int Days, int Max)> OrganizationChatRetentionAsync(CancellationToken ct)
    {
        var ceiling = await OrganizationChatRetentionCeilingAsync(ct);
        var own = (await reader.FindOrganizationAsync(ct))?.ChatRetentionDays;
        return (ceiling.Effective(own), ceiling.Count!.Value);
    }

    /// <summary>El tope del plazo de los chats de la organización activa, leído en cada uso.</summary>
    public Task<ModuleLimitCeiling> OrganizationChatRetentionCeilingAsync(CancellationToken ct) =>
        limits.ResolveAsync(WhatsAppModuleLimitContributor.ChatRetentionDaysKey, tenant.TenantId, ct);
}
