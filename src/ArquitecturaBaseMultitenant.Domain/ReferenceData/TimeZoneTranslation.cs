namespace ArquitecturaBaseMultitenant.Domain.ReferenceData;

/// <summary>Da el nombre de ciudad que se muestra para una zona IANA según la cultura.</summary>
public sealed class TimeZoneTranslation
{
    private TimeZoneTranslation() { }

    public string TimeZoneId { get; private set; } = string.Empty;
    public string Culture { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;

    public static TimeZoneTranslation Create(string timeZoneId, string culture, string city)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        return new TimeZoneTranslation { TimeZoneId = timeZoneId, Culture = culture, City = city };
    }
}
