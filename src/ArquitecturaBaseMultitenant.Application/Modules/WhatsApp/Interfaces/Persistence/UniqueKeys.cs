namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>Claves de deduplicación durable del módulo, compartidas por EF y replay.</summary>
public static class UniqueKeys
{
    public const string MessageEvent = "IX_WhatsAppMessages_PhoneNumberId_Wamid_EventType";
    public const string ContactNumber = "IX_WhatsAppContacts_WaId";
    /// <summary>Un número de Meta está vigente en un solo lugar (organización o Plataforma).</summary>
    public const string ActiveChannelNumber = "IX_WhatsAppChannels_PhoneNumberId_Active";
    public const string ChannelUsageMonth = "IX_WhatsAppChannelUsages_ChannelId_Month";
    /// <summary>Un nombre de equipo por organización, sin distinguir mayúsculas; y uno por nombre en Plataforma.</summary>
    public const string TeamName = "IX_WhatsAppTeams_TenantId_NormalizedName";
    public const string PlatformTeamName = "IX_PlatformWhatsAppTeams_NormalizedName";
    /// <summary>Un nombre de Meta por organización y uno por Plataforma; y un estado por nombre e idioma en cada cuenta.</summary>
    public const string TemplateName = "IX_WhatsAppTemplates_TenantId_MetaName";
    public const string PlatformTemplateName = "IX_PlatformWhatsAppTemplates_MetaName";
    public const string TemplateState = "IX_WhatsAppTemplateStates_WabaId_MetaName_MetaLanguage";
}
