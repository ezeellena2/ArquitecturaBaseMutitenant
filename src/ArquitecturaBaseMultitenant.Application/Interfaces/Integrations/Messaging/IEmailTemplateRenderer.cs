using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;

public interface IEmailTemplateRenderer
{
    EmailMessage RenderLoginCode(string to, string code, int lifetimeMinutes, CultureProfile culture);
    EmailMessage RenderSignupCode(string to, string code, int lifetimeMinutes, CultureProfile culture);
    EmailMessage RenderInvitation(string to, string loginUrl, CultureProfile culture);
    Task<EmailMessage> RenderAccountNoticeAsync(string to, AccountNotice notice, CultureProfile culture,
        CancellationToken cancellationToken);
}
