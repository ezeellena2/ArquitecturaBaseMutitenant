using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
public interface IWhatsAppTemplateRenderer
{
    WhatsAppOutboundMessage RenderCode(string destination, string code, int lifetimeMinutes, CultureProfile culture);
    Task<WhatsAppOutboundMessage> RenderInvitationAsync(string destination, InvitationNotice notice, CultureProfile culture, CancellationToken ct);
    Task<WhatsAppOutboundMessage?> RenderNoticeAsync(string destination, AccountNotice notice, CultureProfile culture, CancellationToken ct);
}
