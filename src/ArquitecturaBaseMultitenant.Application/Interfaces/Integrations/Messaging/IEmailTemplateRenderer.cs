using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;

/// <summary>Compone los correos de ingreso, invitación y avisos con la cultura elegida; el canal de entrega se encarga del envío.</summary>
public interface IEmailTemplateRenderer
{
    EmailMessage RenderLoginCode(string to, string code, int lifetimeMinutes, CultureProfile culture);
    EmailMessage RenderSignupCode(string to, string code, int lifetimeMinutes, CultureProfile culture);
    EmailMessage RenderVerifyEmailCode(string to, string code, int lifetimeMinutes, CultureProfile culture);
    Task<EmailMessage> RenderInvitationAsync(string to, InvitationNotice notice, CultureProfile culture,
        CancellationToken cancellationToken);
    Task<EmailMessage> RenderAccountNoticeAsync(string to, AccountNotice notice, CultureProfile culture,
        CancellationToken cancellationToken);
}
