using ArquitecturaBaseMultitenant.Application.Models.Messaging;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

/// <summary>Separa el envío de un mensaje de correo de la elección entre SMTP y archivos .eml. El canal del outbox depende de este contrato.</summary>
internal interface IEmailTransport
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
