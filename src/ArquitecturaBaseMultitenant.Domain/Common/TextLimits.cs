namespace ArquitecturaBaseMultitenant.Domain.Common;

/// <summary>Centraliza los límites de texto que comparten las reglas de Domain, los validadores de Application y los mapeos de EF.</summary>
public static class TextLimits
{
    public const int PersonName = 100;
    public const int OrganizationName = 120;
    public const int ShortName = 60;
    public const int Description = 500;
    public const int LongText = 4000;
}
