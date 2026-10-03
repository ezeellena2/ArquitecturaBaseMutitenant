namespace ArquitecturaBaseMultitenant.Domain.Common;

/// <summary>Centraliza los límites de texto que comparten las reglas de Domain, los validadores de Application y los mapeos de EF.</summary>
public static class TextLimits
{
    public const int PersonName = 100;
    public const int OrganizationName = 120;
    public const int ShortName = 60;
    public const int Description = 500;
    public const int LongText = 4000;
    public const int AuditSearch = 100;
    public const int TaxIdType = 16;
    public const int TaxIdNumber = 32;
    public const int PhoneNumber = 100;
    public const int WebAddress = 2048;
    public const int ShortCode = 20;
}
