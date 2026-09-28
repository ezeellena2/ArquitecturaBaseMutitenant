namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Phones;

/// <summary>Formats an E.164 number for the country of the display culture.</summary>
public interface IPhoneNumberDisplayFormatter
{
    string Format(string e164, string displayCountryCode);
}
