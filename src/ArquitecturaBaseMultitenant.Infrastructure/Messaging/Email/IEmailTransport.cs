using ArquitecturaBaseMultitenant.Application.Models.Messaging;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

/// <summary>
/// Separa el envío de un mensaje de correo de la elección entre SMTP y archivos .eml. El canal del outbox depende de este
/// contrato, y el transporte clasifica cómo terminó el intento (<see cref="EmailSendOutcome"/>).
/// </summary>
internal interface IEmailTransport
{
    Task<EmailSendOutcome> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
