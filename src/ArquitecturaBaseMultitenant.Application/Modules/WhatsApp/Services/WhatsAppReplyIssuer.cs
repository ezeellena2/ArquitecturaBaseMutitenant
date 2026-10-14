using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Time;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Emite la respuesta libre de una persona dentro de la transacción del servicio: un número desconectado deja el chat para
/// leer (D7), pasadas las 24 horas hace falta una plantilla, y sin medio de pago Meta entrega los de servicio solo dentro de
/// los 1.000 gratis del mes (D1). Encola por el número que recibió la conversación y devuelve el mensaje del outbox.
/// </summary>
internal sealed class WhatsAppReplyIssuer(IWhatsAppChannelRepository channels, ITimeZoneService zones, IOutbox outbox, TimeProvider clock)
{
    internal async Task<Result<Guid>> IssueAsync(WhatsAppChatBase chat, string text, CancellationToken ct)
    {
        if (await channels.GetAsync(chat.ChannelId, ct) is not { IsDisconnected: false } channel) return WhatsAppErrors.ConversationReadOnly;
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        if (chat.WindowOpenUntilUtc is not { } expiresAtUtc || expiresAtUtc <= nowUtc) return WhatsAppErrors.ConversationWindowClosed;
        if (channel.Status == WhatsAppNumberStatus.PaymentMissing)
        {
            var month = WhatsAppChannel.UsageMonth(zones.ConvertToLocal(nowUtc, channel.UsageTimeZoneId));
            if ((await channels.GetUsageAsync(channel.Id, month, ct))?.ServiceDelivered >= WhatsAppFeature.FreeServiceMessagesPerMonth)
                return WhatsAppErrors.ConversationPaymentRequired;
        }
        var message = new WhatsAppOutboundMessage(channel.PhoneNumberId, chat.WaId, string.Empty, string.Empty, [], Body: text,
            ReplyExpiresAtUtc: expiresAtUtc, ChannelId: channel.Id);
        return outbox.Enqueue("whatsapp", JsonSerializer.Serialize(message));
    }
}
