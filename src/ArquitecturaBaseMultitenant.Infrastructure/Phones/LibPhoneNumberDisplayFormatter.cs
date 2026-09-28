using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Phones;
using PhoneNumbers;

namespace ArquitecturaBaseMultitenant.Infrastructure.Phones;

/// <summary>Display formatting from libphonenumber metadata; input is already E.164.</summary>
public sealed class LibPhoneNumberDisplayFormatter : IPhoneNumberDisplayFormatter
{
    private static readonly PhoneNumberUtil Util = PhoneNumberUtil.GetInstance();

    public string Format(string e164, string displayCountryCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(e164);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayCountryCode);

        PhoneNumber number;
        try
        {
            number = Util.Parse(e164, null);
        }
        catch (NumberParseException exception)
        {
            throw new ArgumentException("The phone number must be E.164.", nameof(e164), exception);
        }

        var ownCountry = string.Equals(
            Util.GetRegionCodeForNumber(number), displayCountryCode,
            StringComparison.OrdinalIgnoreCase);
        var formatted = Util.Format(number,
            ownCountry ? PhoneNumberFormat.NATIONAL : PhoneNumberFormat.INTERNATIONAL);

        // The shared presentation contract uses spaces between international groups.
        return ownCountry ? formatted : formatted.Replace('-', ' ');
    }
}
