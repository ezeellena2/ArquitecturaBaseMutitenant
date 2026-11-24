using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

/// <summary>Lee el nombre de la aplicación y el del remitente de las opciones de correo; sin remitente, el de la aplicación.</summary>
internal sealed class EmailBrandingDefaults(IOptions<EmailOptions> email, IOptions<SmtpOptions> smtp) : IEmailBrandingDefaults
{
    public string AppName => email.Value.AppName;

    public string SenderName => string.IsNullOrWhiteSpace(smtp.Value.FromName) ? email.Value.AppName : smtp.Value.FromName;
}
