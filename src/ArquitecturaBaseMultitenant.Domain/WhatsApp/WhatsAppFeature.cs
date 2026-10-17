namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>Llave del módulo de atención por organización y topes que fija Meta o el producto (ADR 0038).</summary>
public static class WhatsAppFeature
{
    public const string Key = "whatsapp";
    public const int FreeServiceMessagesPerMonth = 1000;
    public const int MaxMenuOptions = 10;
    public const int MaxPhoneImport = 200;
    public const int MaxImportRows = 5000;
    public const int MaxTemplateVariables = 10;
    public const int MaxContactTags = 10;
    /// <summary>Integrantes de un equipo: el detalle los trae todos y el PUT reemplaza la lista.</summary>
    public const int MaxTeamMembers = 200;
    public const int MaxTemplateLanguages = 10;
    public const int MaxMemberPreview = 3;
    public static IReadOnlyList<int> NotifyAfterMinutesOptions { get; } = [10, 15, 30];
}

/// <summary>Largos de los textos de atención; los de Meta salen de sus límites publicados.</summary>
public static class WhatsAppTextLimits
{
    public const int MessageText = 4096;
    public const int BotText = 1024;
    public const int MenuLabel = 24;
    public const int TemplateBody = 1024;
    public const int TemplateVariable = 200;
    public const int Tag = 40;
    public const int MetaIdentifier = 32;
    public const int SignupCode = 1024;
}
