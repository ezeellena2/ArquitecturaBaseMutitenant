using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
/// <summary>Configuración funcional del canal; las opciones y la región real del E.164 se resuelven en su adaptador técnico.</summary>
public interface IWhatsAppChannelSettings
{
    bool IsEnabled { get; }
    string AppName { get; }
    string? PhoneNumberId { get; }
    string? ContactPhone { get; }
    IReadOnlyList<string> Countries { get; }
    int DailyAuthCodeLimit { get; }
    decimal ProtectedAuthCodeFraction => 0.2m;
    int DailyNewAuthCodeIpLimit => 20;
    int DailyNewAuthCodeLimit => DailyAuthCodeLimit - (int)Math.Ceiling(DailyAuthCodeLimit * ProtectedAuthCodeFraction);
    Result ValidateNewDestination(LoginCodeDestination destination);
}
