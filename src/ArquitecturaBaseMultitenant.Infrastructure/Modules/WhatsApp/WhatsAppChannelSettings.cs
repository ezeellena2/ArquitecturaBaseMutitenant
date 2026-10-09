using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Options;
using PhoneNumbers;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
internal sealed class WhatsAppChannelSettings(IOptions<WhatsAppOptions> options, IOptions<EmailOptions> email) : IWhatsAppChannelSettings
{
    public string AppName => email.Value.AppName;
    public bool IsEnabled => !string.IsNullOrWhiteSpace(options.Value.PhoneNumberId);
    public string? PhoneNumberId => options.Value.PhoneNumberId;
    public string? ContactPhone
    {
        get
        {
            var raw = options.Value.DisplayPhoneNumber;
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var normalized = string.Concat(raw.Where(value => !char.IsWhiteSpace(value)));
            if (!normalized.StartsWith('+')) normalized = "+" + normalized;
            var phone = ArquitecturaBaseMultitenant.Domain.ValueObjects.PhoneNumber.Create(normalized);
            return phone.IsSuccess ? phone.Value.Value : null;
        }
    }
    public IReadOnlyList<string> Countries => options.Value.AllowedCountries;
    public int DailyAuthCodeLimit => options.Value.DailyAuthCodeLimit;
    public Result ValidateNewDestination(LoginCodeDestination destination)
    {
        if (!IsEnabled || destination.Channel != "whatsapp") return LoginChannelErrors.Unavailable;
        try
        {
            var util = PhoneNumberUtil.GetInstance();
            var region = util.GetRegionCodeForNumber(util.Parse(destination.Value, null));
            return Countries.Contains(region, StringComparer.Ordinal) ? Result.Success()
                : WhatsAppErrors.CountryNotAllowed;
        }
        catch (NumberParseException) { return WhatsAppErrors.CountryNotAllowed; }
    }
}
