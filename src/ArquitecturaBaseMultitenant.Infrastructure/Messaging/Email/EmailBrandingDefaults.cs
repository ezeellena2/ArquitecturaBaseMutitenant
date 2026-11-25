using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

/// <summary>Lee el nombre de la aplicación, el del remitente y el logo de las opciones de correo; sin remitente, el de la aplicación.</summary>
internal sealed class EmailBrandingDefaults(IOptions<EmailOptions> email, IOptions<SmtpOptions> smtp) : IEmailBrandingDefaults
{
    public string AppName => email.Value.AppName;

    public string SenderName => string.IsNullOrWhiteSpace(smtp.Value.FromName) ? email.Value.AppName : smtp.Value.FromName;

    public string? LogoUrl => string.IsNullOrWhiteSpace(email.Value.LogoUrl) ? null : email.Value.LogoUrl;
}
