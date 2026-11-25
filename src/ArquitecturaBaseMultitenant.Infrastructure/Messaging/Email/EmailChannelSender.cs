using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Domain.Messaging;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

/// <summary>
/// Adapta los mensajes de correo del outbox al transporte configurado y traduce cómo terminó cada intento a la cola (N5, N8 y
/// ADR 0066): lo que no salió vuelve a pendiente, un rechazo definitivo falla y lo que pudo haber llegado queda «Sin
/// confirmar». El dispatcher le entrega un payload ya descifrado fuera del request original. El remitente es el de «Nombre y
/// correos» de Plataforma (273), leído antes de cada envío: si no se puede leer, no salió nada y se vuelve a probar.
/// </summary>
internal sealed class EmailChannelSender(IEmailTransport transport, IPlatformSettingsReader? settings = null)
    : IChannelReceiptSender
{
    internal const string NotSentCode = "Email.Smtp.NotSent";
    internal const string CredentialsRejectedCode = "Email.Smtp.CredentialsRejected";
    internal const string RecipientRejectedCode = "Email.Smtp.RecipientRejected";
    internal const string RejectedCode = "Email.Smtp.Rejected";
    internal const string UncertainCode = "Email.Smtp.Uncertain";

    public string Key => OutboxChannel.Email;

    public async Task<OutboxDeliveryReceipt> SendWithReceiptAsync(string payload, CancellationToken cancellationToken)
    {
        EmailMessage? message;
        try { message = JsonSerializer.Deserialize<EmailMessage>(payload); }
        catch (JsonException) { message = null; }
        // Un payload que no se puede leer no sale nunca: falla sin llamar al servidor.
        if (message is null) return OutboxDeliveryReceipt.Rejected(RejectedCode, retryable: false);
        if (settings is not null)
        {
            try { message = message with { FromName = (await settings.FindBrandingAsync(cancellationToken)).SenderName }; }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // Todavía no se llamó al servidor: no salió nada.
                return OutboxDeliveryReceipt.Rejected(NotSentCode, retryable: true);
            }
        }
        return await transport.SendAsync(message, cancellationToken) switch
        {
            EmailSendOutcome.Sent => OutboxDeliveryReceipt.Accepted(),
            EmailSendOutcome.NotSent => OutboxDeliveryReceipt.Rejected(NotSentCode, retryable: true),
            EmailSendOutcome.CredentialsRejected => OutboxDeliveryReceipt.Rejected(CredentialsRejectedCode, retryable: true),
            EmailSendOutcome.RecipientRejected => OutboxDeliveryReceipt.Rejected(RecipientRejectedCode, retryable: false),
            EmailSendOutcome.Rejected => OutboxDeliveryReceipt.Rejected(RejectedCode, retryable: false),
            // Uncertain y cualquier resultado que esta versión no conozca: no se repite.
            _ => OutboxDeliveryReceipt.Uncertain(UncertainCode),
        };
    }

    public async Task SendAsync(string payload, CancellationToken cancellationToken)
    {
        var receipt = await SendWithReceiptAsync(payload, cancellationToken);
        if (!receipt.IsAccepted) throw new InvalidOperationException(receipt.SafeCode);
    }
}
