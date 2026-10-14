using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Lo que escribe un contacto después de una difusión, dentro de la transacción de la entrada: «No recibir más» lo da de
/// baja de las promociones (y de esa difusión); cualquier otra cosa en los 7 días siguientes cuenta como respuesta.
/// </summary>
internal sealed class WhatsAppBroadcastRecipientWriter(IWhatsAppBroadcastRepository broadcasts)
{
    internal static readonly TimeSpan ReplyWindow = TimeSpan.FromDays(7);

    internal async Task RecordInboundAsync(WhatsAppCustomer customer, bool unsubscribe, DateTime atUtc, CancellationToken ct)
    {
        var recipient = await broadcasts.FindLatestSentAsync(customer.Id, atUtc - ReplyWindow, ct);
        if (unsubscribe)
        {
            customer.ChangeConsent(WhatsAppConsent.Unsubscribed, WhatsAppConsentSource.Unsubscribe, atUtc);
            recipient?.MarkUnsubscribed(atUtc);
            return;
        }
        recipient?.MarkReplied(atUtc);
    }
}
