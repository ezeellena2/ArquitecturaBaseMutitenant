using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>
/// La configuración de WhatsApp de la plataforma (decisiones 270 y 272): los países cuyos números reciben códigos, cuánto se
/// guardan los mensajes de los chats de su propio número y el máximo que puede elegir una organización. Una sola fila, global;
/// sin fila valen los valores por defecto y, para los países, la clave <c>WhatsApp:AllowedCountries</c> (0060).
/// </summary>
public sealed class WhatsAppPlatformSettings : Entity, IAuditable, IVersioned
{
    public const string SingletonIdValue = "00000000-0000-0000-0000-00000000a9a2";
    public static readonly Guid SingletonId = new(SingletonIdValue);

    /// <summary>Los países admitidos: de 1 a los de la tabla de países, cada uno una vez.</summary>
    public const int MaxAllowedCountries = 250;

    private WhatsAppPlatformSettings() : base(SingletonId) { }

    /// <summary>Códigos ISO 3166-1 alfa-2 en mayúsculas, sin repetir y en orden.</summary>
    public IReadOnlyList<string> AllowedCountries { get; private set; } = [];
    public int ChatRetentionDays { get; private set; } = WhatsAppSettingsLimits.DefaultChatRetentionDays;
    public int MaxOrganizationChatRetentionDays { get; private set; } = WhatsAppSettingsLimits.DefaultMaxOrganizationChatRetentionDays;
    public uint Version { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }

    /// <summary>La configuración antes de su primer guardado, con los países que regían hasta entonces.</summary>
    public static WhatsAppPlatformSettings Create(IReadOnlyList<string> allowedCountries)
    {
        ArgumentNullException.ThrowIfNull(allowedCountries);
        return new WhatsAppPlatformSettings { AllowedCountries = Normalize(allowedCountries) };
    }

    /// <summary>Aplica los tres valores juntos; nada cambia si uno está fuera de sus cotas.</summary>
    public bool Update(IReadOnlyList<string> allowedCountries, int chatRetentionDays, int maxOrganizationChatRetentionDays)
    {
        ArgumentNullException.ThrowIfNull(allowedCountries);
        if (allowedCountries.Count is 0 or > MaxAllowedCountries || !allowedCountries.All(IsCountryCode)
            || !IsRetention(chatRetentionDays) || !IsRetention(maxOrganizationChatRetentionDays))
            return false;
        AllowedCountries = Normalize(allowedCountries);
        ChatRetentionDays = chatRetentionDays;
        MaxOrganizationChatRetentionDays = maxOrganizationChatRetentionDays;
        return true;
    }

    public static bool IsRetention(int days) =>
        days is >= WhatsAppSettingsLimits.MinChatRetentionDays and <= WhatsAppSettingsLimits.MaxChatRetentionDays;

    /// <summary>Un código como lo guarda la configuración: dos letras ASCII en mayúsculas («AR»).</summary>
    public static bool IsCountryCode(string? country) =>
        country is { Length: 2 } && char.IsAsciiLetterUpper(country[0]) && char.IsAsciiLetterUpper(country[1]);

    private static string[] Normalize(IReadOnlyList<string> countries) =>
        [.. countries.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
}
