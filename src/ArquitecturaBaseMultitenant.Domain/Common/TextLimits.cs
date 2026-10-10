namespace ArquitecturaBaseMultitenant.Domain.Common;

/// <summary>Centraliza los límites de texto que comparten las reglas de Domain, los validadores de Application y los mapeos de EF.</summary>
public static class TextLimits
{
    public const int PersonName = 100;
    public const int FirstName = PersonName;
    public const int LastName = PersonName;
    public const int PersonDisplayName = FirstName + 1 + LastName;
    public const int OrganizationName = 120;
    public const int ShortName = 60;
    public const int Description = 500;
    public const int LongText = 4000;
    public const int AssistantMessage = 8000;
    public const int AssistantTitle = 80;
    /// <summary>El texto de una versión de términos o privacidad, en una cultura.</summary>
    public const int LegalDocument = 100_000;
    public const int AuditSearch = 100;
    public const int TaxIdType = 16;
    public const int TaxIdNumber = 32;
    public const int PhoneNumber = 100;
    public const int WebAddress = 2048;
    public const int ShortCode = 20;
    public const int IpAddress = 45;
}
