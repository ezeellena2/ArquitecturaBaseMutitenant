using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;

public interface IEmailTemplateRenderer
{
    EmailMessage RenderLoginCode(string to, string code, int lifetimeMinutes, CultureInfo culture);
    EmailMessage RenderSignupCode(string to, string code, int lifetimeMinutes, CultureInfo culture);
    EmailMessage RenderInvitation(string to, string loginUrl, CultureInfo culture);
    EmailMessage RenderAccountNotice(string to, AccountNotice notice, CultureInfo culture);
}
