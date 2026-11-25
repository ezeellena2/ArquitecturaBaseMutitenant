using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;

/// <summary>Compone los correos de ingreso, invitación y avisos con la cultura elegida; el canal de entrega se encarga del envío.</summary>
public interface IEmailTemplateRenderer
{
    // El nombre y el logo salen de «Nombre y correos» de Plataforma (273), leídos para cada correo: por eso son async.
    Task<EmailMessage> RenderLoginCodeAsync(string to, string code, int lifetimeMinutes, CultureProfile culture,
        CancellationToken cancellationToken);
    Task<EmailMessage> RenderSignupCodeAsync(string to, string code, int lifetimeMinutes, CultureProfile culture,
        CancellationToken cancellationToken);
    Task<EmailMessage> RenderVerifyEmailCodeAsync(string to, string code, int lifetimeMinutes, CultureProfile culture,
        CancellationToken cancellationToken);
    Task<EmailMessage> RenderInvitationAsync(string to, InvitationNotice notice, CultureProfile culture,
        CancellationToken cancellationToken);
    Task<EmailMessage> RenderAccountNoticeAsync(string to, AccountNotice notice, CultureProfile culture,
        CancellationToken cancellationToken);

    /// <summary>
    /// Un aviso con el armado del tablero Mensajes, con los textos que trae; lo usan los módulos para que sus correos se
    /// vean como los del núcleo.
    /// </summary>
    Task<EmailMessage> RenderNoticeAsync(string to, EmailNotice notice, CultureProfile culture,
        CancellationToken cancellationToken);
}
