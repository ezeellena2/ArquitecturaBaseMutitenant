using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
/// <summary>
/// Configuración funcional del canal de Plataforma; las opciones y la región real del E.164 se resuelven en su adaptador
/// técnico. Desde la E11 manda el número conectado (decisión 3): sin número conectado, códigos y avisos van por correo.
/// </summary>
public interface IWhatsAppChannelSettings
{
    /// <summary>El número de Plataforma está conectado con medio de pago: salen códigos, invitaciones y avisos.</summary>
    bool IsEnabled { get; }
    string AppName { get; }
    /// <summary>El identificador de Meta del número de Plataforma mientras atiende (conectado o sin medio de pago).</summary>
    string? PhoneNumberId { get; }
    string? ContactPhone { get; }
    IReadOnlyList<string> Countries { get; }
    int DailyAuthCodeLimit { get; }
    decimal ProtectedAuthCodeFraction => 0.2m;
    int DailyNewAuthCodeIpLimit => 20;
    int DailyNewAuthCodeLimit => DailyAuthCodeLimit - (int)Math.Ceiling(DailyAuthCodeLimit * ProtectedAuthCodeFraction);
    /// <summary>El número de Plataforma recibe y responde dentro del cupo gratis, aunque le falte el medio de pago.</summary>
    bool IsAttending => IsEnabled;
    Guid? PlatformChannelId => null;
    /// <summary>Plataforma atiende consultas con su número (si no, el bot es el de la E8).</summary>
    bool ConsultationsEnabled => false;
    /// <summary>Datos públicos del alta embebida; null si la instalación no configuró la app de Meta.</summary>
    WhatsAppSignupConfigResponse? Signup => null;
    Result ValidateNewDestination(LoginCodeDestination destination);
}
