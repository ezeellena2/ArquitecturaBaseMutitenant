using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;

/// <summary>
/// El correo «Chats esperando» (o «Consultas esperando» en Plataforma) del tablero Mensajes, en la cultura de quien lo
/// recibe: solo el resumen del equipo y el enlace a la bandeja, nunca teléfonos ni textos de los chats (D6).
/// </summary>
public interface IWhatsAppWaitingEmailRenderer
{
    Task<EmailMessage> RenderAsync(WhatsAppWaitingNotice notice, CancellationToken ct);
}
