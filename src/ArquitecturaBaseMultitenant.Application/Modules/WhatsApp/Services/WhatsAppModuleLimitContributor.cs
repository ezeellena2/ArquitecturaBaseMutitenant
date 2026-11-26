using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Models.Features;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// El tope de WhatsApp con máximo de la plataforma (ADR 0069 §4, 272): el plazo de los chats de una organización, 90 días de
/// entrada y desde 1. Su máximo es el que fija Plataforma en su configuración, leído en cada uso; sin fila, el de defecto. El plazo
/// de los chats de la plataforma no es un tope: lo usa solo la purga de la plataforma.
/// </summary>
internal sealed class WhatsAppModuleLimitContributor(IWhatsAppSettingsReader settings) : IModuleLimitContributor
{
    internal const string ChatRetentionDaysKey = "whatsapp.chatRetentionDays";

    public IReadOnlyList<ModuleLimitDefinition> Limits { get; } =
    [
        new(ChatRetentionDaysKey, ModuleLimitKind.Count, WhatsAppSettingsLimits.DefaultChatRetentionDays,
            WhatsAppSettingsLimits.MinChatRetentionDays),
    ];

    public async Task<ModuleLimitValue> ReadPlatformMaximumAsync(string key, CancellationToken cancellationToken)
    {
        if (key != ChatRetentionDaysKey)
            throw new ArgumentException($"Module limit key '{key}' is not declared by WhatsApp.", nameof(key));
        var platform = await settings.FindPlatformAsync(cancellationToken);
        return new ModuleLimitValue(
            platform?.MaxOrganizationChatRetentionDays ?? WhatsAppSettingsLimits.DefaultMaxOrganizationChatRetentionDays, null);
    }
}
