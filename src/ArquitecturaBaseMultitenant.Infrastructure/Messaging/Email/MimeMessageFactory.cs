using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using MimeKit;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

/// <summary>Construye el mensaje MIME con remitente, destinatario y cuerpos HTML y texto. Lo comparten el transporte SMTP y el pickup para que ambos entreguen el mismo contenido.</summary>
internal static class MimeMessageFactory
{
    public static MimeMessage Create(EmailMessage message, SmtpOptions sender)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(sender.FromName, sender.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody,
        }.ToMessageBody();
        return mime;
    }
}
