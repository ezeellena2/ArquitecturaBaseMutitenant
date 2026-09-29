namespace ArquitecturaBaseMultitenant.Application.Common.Formatting;

/// <summary>Perfil resuelto una vez para componer varios textos de una misma salida.</summary>
public sealed class DisplayFormatContext
{
    internal DisplayFormatContext(CultureProfile profile, string timeZoneId)
    {
        Profile = profile;
        TimeZoneId = timeZoneId;
    }

    internal CultureProfile Profile { get; }

    public string Culture => Profile.Entry.Code;

    public string TimeZoneId { get; }
}
