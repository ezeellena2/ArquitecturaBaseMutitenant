using ArquitecturaBaseMultitenant.Application.Models.Messaging;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;

/// <summary>
/// El correo «Chats esperando» (o «Consultas esperando» en Plataforma) en la cultura de quien lo recibe: solo el equipo, la
/// cantidad y el enlace a la bandeja, nunca teléfonos ni textos de los chats (D6).
/// </summary>
public interface IWhatsAppWaitingEmailRenderer
{
    Task<EmailMessage> RenderAsync(string recipient, string teamName, int waiting, string inboxUrl, bool platform, string culture, string zone,
        CancellationToken ct);
}
