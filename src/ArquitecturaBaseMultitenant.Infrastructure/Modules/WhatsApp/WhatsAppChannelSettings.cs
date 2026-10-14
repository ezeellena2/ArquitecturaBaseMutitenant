using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Options;
using PhoneNumbers;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
/// <summary>El canal de Plataforma sale del número conectado (decisión 3 de la E11); los países y topes, de la configuración.</summary>
internal sealed class WhatsAppChannelSettings(IOptions<WhatsAppOptions> options, IOptions<EmailOptions> email,
    WhatsAppPlatformChannelState state) : IWhatsAppChannelSettings
{
    public string AppName => email.Value.AppName;
    public bool IsEnabled => state.Current is { Status: WhatsAppNumberStatus.Connected };
    public bool IsAttending => state.Current is { Status: not WhatsAppNumberStatus.Disconnected };
    public string? PhoneNumberId => IsAttending ? state.Current?.PhoneNumberId : null;
    public Guid? PlatformChannelId => IsAttending ? state.Current?.Id : null;
    public bool ConsultationsEnabled => IsAttending && state.Current?.ConsultationsEnabled == true;
    public string? ContactPhone => IsAttending ? state.Current?.DisplayNumber : null;
    public WhatsAppSignupConfigResponse? Signup =>
        string.IsNullOrWhiteSpace(options.Value.AppId) || string.IsNullOrWhiteSpace(options.Value.EmbeddedSignupConfigId) ? null
            : new WhatsAppSignupConfigResponse(options.Value.AppId, options.Value.EmbeddedSignupConfigId, options.Value.GraphApiVersion);
    public IReadOnlyList<string> Countries => options.Value.AllowedCountries;
    public int DailyAuthCodeLimit => options.Value.DailyAuthCodeLimit;
    public decimal ProtectedAuthCodeFraction => options.Value.ProtectedAuthCodeFraction;
    public int DailyNewAuthCodeIpLimit => options.Value.DailyNewAuthCodeIpLimit;
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
