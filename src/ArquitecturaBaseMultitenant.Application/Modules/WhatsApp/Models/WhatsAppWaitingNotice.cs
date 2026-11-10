namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;

/// <summary>
/// El aviso «Chats esperando» (o «Consultas esperando» en Plataforma) para una persona del equipo (D6): el equipo, cuántos
/// esperan, hace cuánto espera el más viejo, de qué organización (o la plataforma) y el enlace a la bandeja. Nunca
/// teléfonos ni textos de los chats.
/// </summary>
public sealed record WhatsAppWaitingNotice(string Recipient, string? RecipientName, string TeamName, int Waiting,
    TimeSpan OldestWaiting, string PlaceName, string InboxUrl, bool Platform, string Culture, string TimeZoneId)
{
    public override string ToString() => "WhatsAppWaitingNotice [redacted]";
}
