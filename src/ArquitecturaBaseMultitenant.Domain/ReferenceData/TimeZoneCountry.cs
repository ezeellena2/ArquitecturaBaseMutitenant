namespace ArquitecturaBaseMultitenant.Domain.ReferenceData;

/// <summary>Relaciona una zona IANA con un país y permite habilitar esa opción de forma independiente.</summary>
public sealed class TimeZoneCountry
{
    private TimeZoneCountry() { }

    public string TimeZoneId { get; private set; } = string.Empty;
    public string CountryCode { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }

    public static TimeZoneCountry Create(string timeZoneId, string countryCode, bool isEnabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        return new TimeZoneCountry { TimeZoneId = timeZoneId, CountryCode = countryCode, IsEnabled = isEnabled };
    }
}
